using ConsoleApp1.Entities;
using ConsoleApp1.Tests;
using ConsoleApp1.Validation;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace ConsoleApp1.Validation
{

    public static class ValidatorServiceExtensions
    {
        public static IServiceCollection AddSqlValidators(this IServiceCollection services)
        {
            // Register each validator by its interface and implementation
            // We use Singleton because these validators are stateless and thread-safe

            services.AddTransient<IEntityValidator<SqlExpression>, SqlExpressionValidator>();
            services.AddTransient<IEntityValidator<SelectElement>, SqlSelectElementValidator>();
            services.AddTransient<IEntityValidator<SqlPredicate>, SqlPredicateValidator>();
            services.AddTransient<IEntityValidator<SqlQuery>, SqlQueryValidator>();
            services.AddTransient<SqlValidatorTester>();

            return services;
        }
    }



    public interface IEntityValidator<T>
    {
        (string MemberName, string Message)[] ValidateEntity(T entity);
        (string MemberName, string Message)[] ValidateEntity(T entity, string memberName);
        bool IsEntityValid(T entity);
        bool IsEntityValid(T entity, string memberName);
    }

    public static class ValidationExtensions
    {
        public static void Validate<T>(this IEntityValidator<T> validator, T entity, string errorDescription)
        {
            var errors = validator.ValidateEntity(entity);

            bool isValid = errors.IsValid();

            if (isValid == false)
            {
                string errorMessage = errors.ErrorMessage();
                throw new ValidationException($"{errorDescription}: {errorMessage}");
            }
        }

        public static Dictionary<string, string> ToErrorDictionary(this (string MemberName, string Message)[] errors)
        {
            var dict = errors
                .GroupBy(message => message.MemberName)
                .ToDictionary(grp => grp.Key, grp => String.Join(", ", grp.Select(m => m.Message)));

            return dict;
        }

        public static bool IsValid(this (string MemberName, string Message)[] errors)
        {
            bool isValid = errors.Length == 0;
            return isValid;
        }

        public static string ErrorMessage(this (string MemberName, string Message)[] errors)
        {
            var values = errors
                .ToErrorDictionary()
                .Select(item => $"{item.Key}: {item.Value}");

            string errorMessage = String.Join(", ", values);

            return errorMessage;
        }
    }

    public abstract class ValidatorBase<T> : AbstractValidator<T>, IEntityValidator<T>
    {
        public bool IsEntityValid(T entity)
        {
            var isValid = this.Validate(entity).IsValid;

            return isValid;
        }

        public bool IsEntityValid(T entity, string memberName)
        {
            var isValid = this.Validate(entity, o => o.IncludeProperties(memberName)).IsValid;

            return isValid;
        }

        public (string MemberName, string Message)[] ValidateEntity(T entity)
        {
            var messages = this.Validate(entity)
                .Errors
                .Select(e => (e.PropertyName, e.ErrorMessage))
                .ToArray();

            return messages;
        }

        public (string MemberName, string Message)[] ValidateEntity(T entity, string memberName)
        {
            var messages = this.Validate(entity, o => o.IncludeProperties(memberName))
                .Errors
                .Select(e => (e.PropertyName, e.ErrorMessage))
                .ToArray();

            return messages;
        }
    }
}
