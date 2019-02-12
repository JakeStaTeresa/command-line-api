﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine.Invocation;
using FluentAssertions;
using System.Linq;
using System.Threading;
using Xunit;

namespace System.CommandLine.Tests.Binding
{
    public class TypeBinderTests
    {
        public class BuildOptions
        {
            [Fact]
            public void Single_character_constructor_arguments_generate_aliases_with_a_single_dash_prefix()
            {
                var binder = new TypeBinder(typeof(ClassWithSingleLetterCtorParameter));

                var options = binder.BuildOptions().ToArray();

                options.Should().Contain(o => o.HasRawAlias("-x"));
                options.Should().Contain(o => o.HasRawAlias("-y"));
            }

            [Fact]
            public void Multi_character_constructor_arguments_generate_aliases_that_accept_a_double_dash_prefix()
            {
                var binder = new TypeBinder(typeof(ClassWithMultiLetterCtorParameters));

                var options = binder.BuildOptions().ToArray();

                options.Should().Contain(o => o.HasRawAlias("--int-option"));
                options.Should().Contain(o => o.HasRawAlias("--string-option"));
                options.Should().Contain(o => o.HasRawAlias("--bool-option"));
            }

            [Fact]
            public void Single_character_setters_generate_aliases_that_accept_a_single_dash_prefix()
            {
                var binder = new TypeBinder(typeof(ClassWithSingleLetterProperty));

                var options = binder.BuildOptions().ToArray();

                options.Should().Contain(o => o.HasRawAlias("-x"));
                options.Should().Contain(o => o.HasRawAlias("-y"));
            }

            [Fact]
            public void Multi_character_setters_generate_aliases_that_accept_a_single_dash_prefix()
            {
                var binder = new TypeBinder(typeof(ClassWithMultiLetterSetters));

                var options = binder.BuildOptions().ToArray();

                options.Should().Contain(o => o.HasRawAlias("--int-option"));
                options.Should().Contain(o => o.HasRawAlias("--string-option"));
                options.Should().Contain(o => o.HasRawAlias("--bool-option"));
            }

            [Fact]
            public void When_both_constructor_parameters_and_setters_are_present_then_BuildOptions_creates_options_for_all_of_them()
            {
                var binder = new TypeBinder(typeof(ClassWithSettersAndCtorParametersWithDifferentNames));

                var options = binder.BuildOptions();

                options.Should().Contain(o => o.HasRawAlias("--int-option"));
                options.Should().Contain(o => o.HasRawAlias("--string-option"));
                options.Should().Contain(o => o.HasRawAlias("--bool-option"));

                options.Should().Contain(o => o.HasRawAlias("-i"));
                options.Should().Contain(o => o.HasRawAlias("-s"));
                options.Should().Contain(o => o.HasRawAlias("-b"));
            }

            [Fact]
            public void Default_option_values_are_based_on_constructor_parameter_defaults()
            {
                var binder = new TypeBinder(typeof(ClassWithMultiLetterCtorParameters));

                var options = binder.BuildOptions().ToArray();

                options.Single(o => o.HasRawAlias("--int-option"))
                       .Argument
                       .GetDefaultValue()
                       .Should()
                       .Be(123);

                options.Single(o => o.HasRawAlias("--string-option"))
                       .Argument
                       .GetDefaultValue()
                       .Should()
                       .Be("the default");
            }

            [Theory]
            [InlineData(typeof(IConsole))]
            [InlineData(typeof(InvocationContext))]
            [InlineData(typeof(ParseResult))]
            [InlineData(typeof(CancellationToken))]
            public void Options_are_not_built_for_infrastructure_types_exposed_by_properties(Type type)
            {
                var binder = new TypeBinder(typeof(ClassWithSetter<>).MakeGenericType(type));

                var options = binder.BuildOptions();

                options.Should()
                       .NotContain(o => o.Argument.ArgumentType == type);
            }
        }

        public class CreateInstance
        {
            [Fact]
            public void Option_arguments_are_bound_by_name_to_constructor_parameters()
            {
                var argument = new Argument<string>("the default");

                var option = new Option("--string-option",
                                        argument: argument);

                var command = new Command("the-command");
                command.AddOption(option);
                var binder = new TypeBinder(typeof(ClassWithMultiLetterCtorParameters));

                var parser = new Parser(command);
                var invocationContext = new InvocationContext(
                    parser.Parse("--string-option not-the-default"));

                var instance = (ClassWithMultiLetterCtorParameters)binder.CreateInstance(invocationContext);

                instance.StringOption.Should().Be("not-the-default");
            }

            [Theory]
            [InlineData(typeof(string), "hello", "hello")]
            [InlineData(typeof(int), "123", 123)]
            public void Command_arguments_are_bound_by_name_to_constructor_parameters(
                Type type,
                string commandLine,
                object expectedValue)
            {
                var targetType = typeof(ClassWithCtorParameter<>).MakeGenericType(type);
                var binder = new TypeBinder(targetType);

                var command = new Command("the-command")
                              {
                                  Argument = new Argument
                                             {
                                                 Name = "value",
                                                 ArgumentType = type
                                             }
                              };
                var parser = new Parser(command);

                var invocationContext = new InvocationContext(parser.Parse(commandLine));

                var instance = binder.CreateInstance(invocationContext);

                object valueReceivedValue = ((dynamic)instance).Value;

                valueReceivedValue.Should().Be(expectedValue);
            }

            [Fact]
            public void Explicitly_configured_default_values_can_be_bound_to_constructor_parameters()
            {
                var argument = new Argument<string>("the default");

                var option = new Option("--string-option",
                                        argument: argument);

                var command = new Command("the-command");
                command.AddOption(option);
                var binder = new TypeBinder(typeof(ClassWithMultiLetterCtorParameters));

                var parser = new Parser(command);
                var invocationContext = new InvocationContext(
                    parser.Parse(""));

                var instance = (ClassWithMultiLetterCtorParameters)binder.CreateInstance(invocationContext);

                instance.StringOption.Should().Be("the default");
            }

            [Fact]
            public void Option_arguments_are_bound_by_name_to_property_setters()
            {
                var argument = new Argument<bool>();

                var option = new Option("--bool-option",
                                        argument: argument);

                var command = new Command("the-command");
                command.AddOption(option);
                var binder = new TypeBinder(typeof(ClassWithMultiLetterSetters));

                var parser = new Parser(command);
                var invocationContext = new InvocationContext(
                    parser.Parse("--bool-option"));

                var instance = (ClassWithMultiLetterSetters)binder.CreateInstance(invocationContext);

                instance.BoolOption.Should().BeTrue();
            }

            [Theory]
            [InlineData(typeof(string), "hello", "hello")]
            [InlineData(typeof(int), "123", 123)]
            public void Command_arguments_are_bound_by_name_to_property_setters(
                Type type,
                string commandLine,
                object expectedValue)
            {
                var targetType = typeof(ClassWithSetter<>).MakeGenericType(type);
                var binder = new TypeBinder(targetType);

                var command = new Command("the-command")
                              {
                                  Argument = new Argument
                                             {
                                                 Name = "value",
                                                 ArgumentType = type
                                             }
                              };
                var parser = new Parser(command);

                var invocationContext = new InvocationContext(parser.Parse(commandLine));

                var instance = binder.CreateInstance(invocationContext);

                object valueReceivedValue = ((dynamic)instance).Value;

                valueReceivedValue.Should().Be(expectedValue);
            }

            [Fact]
            public void Explicitly_configured_default_values_can_be_bound_to_property_setters()
            {
                var argument = new Argument<string>("the default");

                var option = new Option("--string-option",
                                        argument: argument);

                var command = new Command("the-command");
                command.AddOption(option);
                var binder = new TypeBinder(typeof(ClassWithMultiLetterSetters));

                var parser = new Parser(command);
                var invocationContext = new InvocationContext(
                    parser.Parse(""));

                var instance = (ClassWithMultiLetterSetters)binder.CreateInstance(invocationContext);

                instance.StringOption.Should().Be("the default");
            }

            [Fact]
            public void Property_setters_with_no_default_value_and_no_matching_option_are_not_called()
            {
                var command = new Command("the-command");

                var binder = new TypeBinder(typeof(ClassWithSettersAndCtorParametersWithDifferentNames));

                foreach (var option in binder.BuildOptions())
                {
                    command.Add(option);
                }

                var parser = new Parser(command);
                var invocationContext = new InvocationContext(
                    parser.Parse(""));

                var instance = (ClassWithSettersAndCtorParametersWithDifferentNames)binder.CreateInstance(invocationContext);

                instance.StringOption.Should().Be("the default");
            }
        }
    }
}
