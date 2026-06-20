using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Threading;

namespace Lithnet.Ecma2Framework.Shim
{
    /// <summary>
    /// Launches and owns the net8 worker process, assigning it to a Job Object so that it is
    /// unconditionally terminated when this host is disposed.
    /// </summary>
    /// <remarks>
    /// The shim acts as the named-pipe server and the worker as the client. The caller must create the
    /// <see cref="JsonRpcPipeClient"/> (and start listening) before calling <see cref="Start"/>, so the
    /// pipe is ready before the worker attempts to connect.
    ///
    /// Orphan-prevention: the worker is created with <c>CREATE_SUSPENDED</c>, assigned to the Job Object
    /// while still suspended, and only then resumed. There is therefore no instant at which the worker is
    /// running yet outside the job, so a host crash between create and assign can never leave an orphaned
    /// worker. Once running, the worker also self-exits when the pipe breaks (host death), so the orphan
    /// window is closed from both ends.
    ///
    /// The worker's standard streams are deliberately NOT redirected. Redirecting a child stream that the
    /// shim never drains would deadlock the worker once the OS pipe buffer filled; and plumbing inheritable
    /// std handles from a service host (miiserver) whose own std handles may be invalid is fragile. Worker
    /// diagnostics are the worker's own responsibility (its logging sinks), not the shim's to capture.
    /// </remarks>
    internal sealed class WorkerProcessHost : IDisposable
    {
        private readonly string workerExePath;
        private readonly string pipeName;

        private JobObject job;
        private SafeFileHandle processHandle;
        private bool disposed;

        /// <param name="workerExePath">Absolute path to the net8 worker executable.</param>
        /// <param name="pipeName">Name of the named pipe the worker should connect to.</param>
        public WorkerProcessHost(string workerExePath, string pipeName)
        {
            if (workerExePath == null)
            {
                throw new ArgumentNullException("workerExePath");
            }

            if (pipeName == null)
            {
                throw new ArgumentNullException("pipeName");
            }

            this.workerExePath = workerExePath;
            this.pipeName = pipeName;
        }

        /// <summary>
        /// Creates the Job Object, launches the worker suspended, assigns it to the job, and resumes it.
        /// </summary>
        /// <remarks>
        /// Error conditions:
        /// <list type="bullet">
        ///   <item>Throws <see cref="Win32Exception"/> if the Job Object cannot be created or configured.</item>
        ///   <item>Throws <see cref="Win32Exception"/> if <c>CreateProcess</c> fails.</item>
        ///   <item>Throws <see cref="Win32Exception"/> if <c>AssignProcessToJobObject</c> or
        ///     <c>ResumeThread</c> fails; the suspended worker is terminated before the exception
        ///     propagates so it is never leaked.</item>
        /// </list>
        /// </remarks>
        public unsafe void Start()
        {
            this.job = new JobObject();

            // CreateProcessW may write to the command-line buffer, so it must be a mutable, null-terminated
            // copy. When lpApplicationName is supplied, the command line's first token is argv[0] by
            // convention, so the (quoted) executable path is included as the first token.
            string commandLine = "\"" + this.workerExePath + "\" --pipe " + this.pipeName;
            char[] commandLineBuffer = new char[commandLine.Length + 1];
            commandLine.CopyTo(0, commandLineBuffer, 0, commandLine.Length);

            STARTUPINFOW startupInfo = new STARTUPINFOW();
            startupInfo.cb = (uint)sizeof(STARTUPINFOW);

            PROCESS_INFORMATION processInfo;
            bool created;

            fixed (char* commandLinePtr = commandLineBuffer)
            {
                created = PInvoke.CreateProcess(
                    this.workerExePath,
                    new PWSTR(commandLinePtr),
                    null,
                    null,
                    false,
                    PROCESS_CREATION_FLAGS.CREATE_SUSPENDED | PROCESS_CREATION_FLAGS.CREATE_NO_WINDOW,
                    null,
                    null,
                    in startupInfo,
                    out processInfo);
            }

            if (!created)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateProcess failed for the worker process.");
            }

            SafeFileHandle processSafeHandle = new SafeFileHandle((IntPtr)processInfo.hProcess.Value, true);
            SafeFileHandle threadSafeHandle = new SafeFileHandle((IntPtr)processInfo.hThread.Value, true);

            try
            {
                // Assign the still-suspended worker to the job, then resume it.
                this.job.Assign(processSafeHandle);

                if (PInvoke.ResumeThread(threadSafeHandle) == unchecked((uint)-1))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "ResumeThread failed for the worker process.");
                }
            }
            catch
            {
                // The worker is suspended and could not be assigned or resumed. Terminate it deterministically
                // rather than leak a suspended process, then surface the failure.
                PInvoke.TerminateProcess(processSafeHandle, 1);
                processSafeHandle.Dispose();
                threadSafeHandle.Dispose();
                throw;
            }

            // The thread handle is no longer needed once the worker is running.
            threadSafeHandle.Dispose();

            this.processHandle = processSafeHandle;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;

            // Disposing the job terminates the worker via JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE.
            if (this.job != null)
            {
                this.job.Dispose();
            }

            // Release the process handle after the job has been closed.
            if (this.processHandle != null)
            {
                this.processHandle.Dispose();
            }
        }
    }
}
