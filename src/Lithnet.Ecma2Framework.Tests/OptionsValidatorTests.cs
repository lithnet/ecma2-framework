using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Lithnet.Ecma2Framework.Internal;
using Microsoft.MetadirectoryServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lithnet.Ecma2Framework.Tests
{
    /// <summary>
    /// Tests for <see cref="OptionsValidator.ValidateObject"/>'s robustness on the validation-failure path.
    /// A failure must always carry the real validation message and must never crash while resolving the
    /// UI parameter name — including the two awkward cases a consumer can legitimately produce: a
    /// class-level <see cref="IValidatableObject"/> failure with no member names, and a failing member that
    /// is not a property decorated as a config parameter.
    /// </summary>
    [TestClass]
    public class OptionsValidatorTests
    {
        [TestMethod]
        public void ClassLevelFailureWithEmptyMemberNames_ReturnsFailure_DoesNotThrow()
        {
            ParameterValidationResult result = OptionsValidator.ValidateObject(new ClassLevelOptions(), null);

            Assert.AreEqual(ParameterValidationResultCode.Failure, result.Code, "A class-level failure must be reported as Failure.");
            Assert.AreEqual("Cross-field validation failed.", result.ErrorMessage, "The real validation message must be carried.");
            Assert.AreEqual(string.Empty, result.ErrorParameter, "With no member names, the parameter name must fall back to empty rather than crash.");
        }

        [TestMethod]
        public void FailingPropertyWithoutDataParameterAttribute_FallsBackToMemberName()
        {
            ParameterValidationResult result = OptionsValidator.ValidateObject(new NoAttributeOptions(), null);

            Assert.AreEqual(ParameterValidationResultCode.Failure, result.Code, "A missing required value must be reported as Failure.");
            Assert.AreEqual("Name", result.ErrorParameter, "A failing member with no DataParameter attribute must fall back to the raw member name.");
        }

        [TestMethod]
        public void ValidObject_ReturnsSuccess()
        {
            ParameterValidationResult result = OptionsValidator.ValidateObject(new NoAttributeOptions { Name = "value" }, null);

            Assert.AreEqual(ParameterValidationResultCode.Success, result.Code, "A valid object must be reported as Success.");
        }

        private sealed class NoAttributeOptions
        {
            [Required]
            public string Name { get; set; }
        }

        private sealed class ClassLevelOptions : IValidatableObject
        {
            public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
            {
                yield return new ValidationResult("Cross-field validation failed.");
            }
        }
    }
}
