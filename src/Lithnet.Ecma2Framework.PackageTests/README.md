# Lithnet.Ecma2Framework.PackageTests

A connector that consumes the packed framework packages instead of the projects in this repository. The end-to-end test packs the three framework packages locally, restores them, and builds the host and shim, proving a clean package consumer can build a management agent from the package alone. Targets net8.0.
