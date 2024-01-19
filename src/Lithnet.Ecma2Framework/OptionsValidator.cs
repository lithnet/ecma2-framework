using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public static class OptionsValidator
    {
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

            var result = results.First();
            string message = result.ErrorMessage;
            var property = objectToValidate.GetType().GetProperty(result.MemberNames.First());
            string paramName = property.GetCustomAttributes<DataParameterAttribute>(true).FirstOrDefault().Name;
            return new ParameterValidationResult(ParameterValidationResultCode.Failure, message, paramName);
        }
    }
}
