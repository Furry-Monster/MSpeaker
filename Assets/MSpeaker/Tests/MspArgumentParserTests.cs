using System.Collections.Generic;
using NUnit.Framework;
using MSpeaker.Runtime.Parser;

namespace MSpeaker.Tests
{
    public class MspArgumentParserTests
    {
        [Test]
        public void ParseArguments_EmptyOrWhitespace_ReturnsEmptyList()
        {
            Assert.IsEmpty(MspArgumentParser.ParseArguments(null));
            Assert.IsEmpty(MspArgumentParser.ParseArguments(""));
            Assert.IsEmpty(MspArgumentParser.ParseArguments("   "));
        }

        [Test]
        public void ParseArguments_SingleInteger_ReturnsOneIntegerArgument()
        {
            var args = MspArgumentParser.ParseArguments("42");
            Assert.AreEqual(1, args.Count);
            Assert.AreEqual(MspArgumentType.Integer, args[0].Type);
            Assert.AreEqual(42, args[0].ConvertedValue);
        }

        [Test]
        public void ParseArguments_SingleFloat_ReturnsOneFloatArgument()
        {
            var args = MspArgumentParser.ParseArguments("-3.14");
            Assert.AreEqual(1, args.Count);
            Assert.AreEqual(MspArgumentType.Float, args[0].Type);
            Assert.AreEqual(-3.14f, (float)args[0].ConvertedValue, 0.0001f);
        }

        [Test]
        public void ParseArguments_SingleBoolean_ReturnsOneBooleanArgument()
        {
            var args = MspArgumentParser.ParseArguments("true");
            Assert.AreEqual(1, args.Count);
            Assert.AreEqual(MspArgumentType.Boolean, args[0].Type);
            Assert.AreEqual(true, args[0].ConvertedValue);

            args = MspArgumentParser.ParseArguments("false");
            Assert.AreEqual(1, args.Count);
            Assert.AreEqual(false, args[0].ConvertedValue);
        }

        [Test]
        public void ParseArguments_QuotedString_ReturnsOneStringArgument()
        {
            var args = MspArgumentParser.ParseArguments("\"hello world\"");
            Assert.AreEqual(1, args.Count);
            Assert.AreEqual(MspArgumentType.String, args[0].Type);
            Assert.AreEqual("hello world", args[0].ConvertedValue);
        }

        [Test]
        public void ParseArguments_Variable_ReturnsVariableType()
        {
            var args = MspArgumentParser.ParseArguments("$playerName");
            Assert.AreEqual(1, args.Count);
            Assert.AreEqual(MspArgumentType.Variable, args[0].Type);
            Assert.AreEqual("playerName", args[0].ConvertedValue);
        }

        [Test]
        public void ParseArguments_CommaSeparatedMultipleArgs_ParsesAll()
        {
            var args = MspArgumentParser.ParseArguments("hello, 123, true");
            Assert.AreEqual(3, args.Count);
            Assert.AreEqual("hello", args[0].ConvertedValue);
            Assert.AreEqual(123, args[1].ConvertedValue);
            Assert.AreEqual(true, args[2].ConvertedValue);
        }

        [Test]
        public void ParseCondition_EmptyOrWhitespace_ReturnsInvalid()
        {
            var info = MspArgumentParser.ParseCondition(null);
            Assert.IsFalse(info.IsValid);

            info = MspArgumentParser.ParseCondition("   ");
            Assert.IsFalse(info.IsValid);
        }

        [Test]
        public void ParseCondition_ComparisonExpression_ParsesOperatorAndOperands()
        {
            var info = MspArgumentParser.ParseCondition("score >= 10");
            Assert.IsTrue(info.IsValid);
            Assert.AreEqual("score", info.LeftOperand);
            Assert.AreEqual(">=", info.Operator);
            Assert.AreEqual("10", info.RightOperand);

            info = MspArgumentParser.ParseCondition("$health < 50");
            Assert.IsTrue(info.IsValid);
            Assert.AreEqual("$health", info.LeftOperand);
            Assert.AreEqual("<", info.Operator);
            Assert.AreEqual("50", info.RightOperand);
        }

        [Test]
        public void ParseCondition_Equality_ParsesCorrectly()
        {
            var info = MspArgumentParser.ParseCondition("name == \"Alice\"");
            Assert.IsTrue(info.IsValid);
            Assert.AreEqual("name", info.LeftOperand);
            Assert.AreEqual("==", info.Operator);
            Assert.AreEqual("Alice", info.RightOperand);
        }

        [Test]
        public void ParseCondition_SingleExpression_TreatedAsTruthCheck()
        {
            var info = MspArgumentParser.ParseCondition("someVar");
            Assert.IsTrue(info.IsValid);
            Assert.AreEqual("someVar", info.LeftOperand);
            Assert.AreEqual("==", info.Operator);
            Assert.AreEqual("true", info.RightOperand);
        }
    }
}
