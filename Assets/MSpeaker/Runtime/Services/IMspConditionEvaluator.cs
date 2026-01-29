namespace MSpeaker.Runtime.Services
{
    public interface IMspConditionEvaluator
    {
        bool Evaluate(string expression);
        bool EvaluateChoice(string conditionExpression);
    }
}