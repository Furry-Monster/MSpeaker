using NUnit.Framework;
using UnityEngine;
using MSpeaker.Runtime;
using MSpeaker.Runtime.Parser;

namespace MSpeaker.Tests
{
    public class MspDialogueParserTests
    {
        private static MspDialogueAsset CreateAsset(string content)
        {
            var asset = ScriptableObject.CreateInstance<MspDialogueAsset>();
            asset.Content = content;
            return asset;
        }

        [Test]
        public void Parse_MinimalSingleConversation_ParsesOneConversation()
        {
            var asset = CreateAsset(@"{{DialogueName(Main)}}
[Speaker]
你好。");
            var conversations = MspDialogueParser.Parse(asset);
            Assert.AreEqual(1, conversations.Count);
            Assert.AreEqual("Main", conversations[0].Name);
            Assert.AreEqual(1, conversations[0].Lines.Count);
            Assert.AreEqual(MspLineType.Normal, conversations[0].Lines[0].LineType);
            Assert.AreEqual("Speaker", conversations[0].Lines[0].Speaker);
            Assert.AreEqual("你好。", conversations[0].Lines[0].LineContent.Text);
        }

        [Test]
        public void Parse_TwoConversations_ParsesBoth()
        {
            var asset = CreateAsset(@"{{DialogueName(Start)}}
[Speaker]
第一段。

{{DialogueName(Next)}}
[Player]
第二段。");
            var conversations = MspDialogueParser.Parse(asset);
            Assert.AreEqual(2, conversations.Count);
            Assert.AreEqual("Start", conversations[0].Name);
            Assert.AreEqual("Next", conversations[1].Name);
            Assert.AreEqual("第一段。", conversations[0].Lines[0].LineContent.Text);
            Assert.AreEqual("第二段。", conversations[1].Lines[0].LineContent.Text);
        }

        [Test]
        public void Parse_Choice_ParsesChoiceAndTarget()
        {
            var asset = CreateAsset(@"{{DialogueName(Main)}}
[Speaker]
选一个。

- 选项A -> ConvA
- 选项B -> ConvB");
            var conversations = MspDialogueParser.Parse(asset);
            Assert.AreEqual(1, conversations.Count);
            Assert.IsNotNull(conversations[0].Choices);
            Assert.AreEqual(2, conversations[0].Choices.Count);
        }

        [Test]
        public void Parse_IfElseEndIf_ParsesConditionalBlocks()
        {
            var asset = CreateAsset(@"{{DialogueName(Main)}}
{{If(score >= 10)}}
[Speaker]
高分。
{{Else}}
[Speaker]
低分。
{{EndIf}}");
            var conversations = MspDialogueParser.Parse(asset);
            Assert.AreEqual(1, conversations.Count);
            Assert.IsNotNull(conversations[0].ConditionalBlocks);
            Assert.Greater(conversations[0].Lines.Count, 0);
        }

        [Test]
        public void Parse_LabelAndGoto_ParsesLabelLine()
        {
            var asset = CreateAsset(@"{{DialogueName(Main)}}
{{Label(CheckPoint)}}
[Speaker]
标记点。");
            var conversations = MspDialogueParser.Parse(asset);
            Assert.AreEqual(1, conversations.Count);
            Assert.IsNotNull(conversations[0].Labels);
            Assert.AreEqual(1, conversations[0].Labels.Count);
            Assert.IsTrue(conversations[0].Labels.ContainsKey("CheckPoint"));
        }

        [Test]
        public void Parse_CommentLine_Ignored()
        {
            var asset = CreateAsset(@"{{DialogueName(Main)}}
# 注释行
[Speaker]
内容。");
            var conversations = MspDialogueParser.Parse(asset);
            Assert.AreEqual(1, conversations.Count);
            Assert.AreEqual(1, conversations[0].Lines.Count);
            Assert.AreEqual("内容。", conversations[0].Lines[0].LineContent.Text);
        }

        [Test]
        public void Parse_NullAsset_ThrowsArgumentNullException()
        {
            Assert.Throws<System.ArgumentNullException>(() => MspDialogueParser.Parse(null));
        }

        [Test]
        public void Parse_EmptyContent_ThrowsArgumentException()
        {
            var asset = CreateAsset("");
            Assert.Throws<System.ArgumentException>(() => MspDialogueParser.Parse(asset));
        }
    }
}