using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Internal
{
    /// <summary>
    /// A class used to validate the configuration when the options model is in use
    /// </summary>
    public static class OptionsValidator
    {
        /// <summary>
        /// Validates the configuration parameters
        /// </summary>
        /// <param name="objectToValidate">An options object to validate</param>
        /// <param name="serviceProvider">The service provider</param>
        /// <returns>A ParameterValidationResult object containing the results of the validation</returns>
        public static ParameterValidationResult ValidateObject(object objectToValidate, IServiceProvider serviceProvider)
        {
            if (objectToValidate == null)
            {
                return new ParameterValidationResult();
            }

            ValidationContext vc = new ValidationContext(objectToValidate, serviceProvider, null);
            ICollection<ValidationResult> results = new List<ValidationResult>();
            bool isValid = Validator.TryValidateObject(objectToValidate, vc, results, true);

            if (isValid || results.Count == 0)
            {
                return new ParameterValidationResult();
            }

            // Always carry the real validation message so the failure is never masked. The parameter name
            // is only a UI hint for which field to highlight; resolve it defensively because a class-level
            // IValidatableObject failure may carry no member names, and a validated member may not be a
            // property surfaced as a DataParameter.
            ValidationResult result = results.First();

            string message = string.IsNullOrEmpty(result.ErrorMessage)
                ? "The configuration is not valid."
                : result.ErrorMessage;

            string paramName = ResolveParameterName(objectToValidate, result);

            return new ParameterValidationResult(ParameterValidationResultCode.Failure, message, paramName);
        }

        /// <summary>
        /// Resolves the UI parameter name for a failed validation result. Falls back to the raw member
        /// name, then to an empty string, when the failing member has no name, is not a property, or is a
        /// property not decorated with a <see cref="DataParameterAttribute"/>.
        /// </summary>
        /// <param name="objectToValidate">The options object that was validated.</param>
        /// <param name="result">The validation failure.</param>
        /// <returns>The UI parameter name, the raw member name, or an empty string.</returns>
        private static string ResolveParameterName(object objectToValidate, ValidationResult result)
        {
            string memberName = result.MemberNames == null ? null : result.MemberNames.FirstOrDefault();

            if (string.IsNullOrEmpty(memberName))
            {
                return string.Empty;
            }

            PropertyInfo property = objectToValidate.GetType().GetProperty(memberName);

            if (property == null)
            {
                return memberName;
            }

            DataParameterAttribute attribute = property.GetCustomAttributes<DataParameterAttribute>(true).FirstOrDefault();

            if (attribute == null)
            {
                return memberName;
            }

            return attribute.Name;
        }
    }
}
