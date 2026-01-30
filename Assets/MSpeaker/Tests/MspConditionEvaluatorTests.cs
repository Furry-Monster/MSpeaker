using NUnit.Framework;
using MSpeaker.Runtime.Services;

namespace MSpeaker.Tests
{
    public class MspConditionEvaluatorTests
    {
        private MspVariableService _variableService;
        private MspConditionEvaluator _evaluator;

        [SetUp]
        public void SetUp()
        {
            _variableService = new MspVariableService();
            _evaluator = new MspConditionEvaluator(_variableService);
        }

        [Test]
        public void Evaluate_EmptyOrWhitespace_ReturnsTrue()
        {
            Assert.IsTrue(_evaluator.Evaluate(null));
            Assert.IsTrue(_evaluator.Evaluate(""));
            Assert.IsTrue(_evaluator.Evaluate("   "));
        }

        [Test]
        public void Evaluate_NumericComparison()
        {
            _variableService.SetValue("score", "100");
            Assert.IsTrue(_evaluator.Evaluate("score >= 10"));
            Assert.IsTrue(_evaluator.Evaluate("score > 50"));
            Assert.IsFalse(_evaluator.Evaluate("score < 50"));
            Assert.IsTrue(_evaluator.Evaluate("score == 100"));
            Assert.IsTrue(_evaluator.Evaluate("score != 0"));
        }

        [Test]
        public void Evaluate_StringEquality()
        {
            _variableService.SetValue("name", "Alice");
            Assert.IsTrue(_evaluator.Evaluate("name == \"Alice\""));
            Assert.IsFalse(_evaluator.Evaluate("name == \"Bob\""));
        }

        [Test]
        public void Evaluate_VariableNotSet_ComparisonReturnsFalse()
        {
            _variableService.Clear();
            Assert.IsFalse(_evaluator.Evaluate("missingVar == \"x\""));
        }

        [Test]
        public void EvaluateChoice_Empty_ReturnsTrue()
        {
            Assert.IsTrue(_evaluator.EvaluateChoice(null));
            Assert.IsTrue(_evaluator.EvaluateChoice(""));
        }

        [Test]
        public void EvaluateChoice_DollarVar_ChecksVariableExists()
        {
            Assert.IsFalse(_evaluator.EvaluateChoice("$flag"));
            _variableService.SetValue("flag", "1");
            Assert.IsTrue(_evaluator.EvaluateChoice("$flag"));
        }

        [Test]
        public void EvaluateChoice_Expression_DelegatesToEvaluate()
        {
            _variableService.SetValue("x", "5");
            Assert.IsTrue(_evaluator.EvaluateChoice("x >= 1"));
        }
    }
}
