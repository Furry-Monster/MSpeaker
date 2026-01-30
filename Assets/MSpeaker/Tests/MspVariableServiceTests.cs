using NUnit.Framework;
using MSpeaker.Runtime.Services;

namespace MSpeaker.Tests
{
    public class MspVariableServiceTests
    {
        private MspVariableService _service;

        [SetUp]
        public void SetUp()
        {
            _service = new MspVariableService();
        }

        [Test]
        public void GetString_EmptyKey_ReturnsDefault()
        {
            Assert.IsNull(_service.GetString(""));
            Assert.IsNull(_service.GetString(null));
            Assert.AreEqual("default", _service.GetString("", "default"));
        }

        [Test]
        public void GetString_NotSet_ReturnsDefault()
        {
            Assert.IsNull(_service.GetString("missing"));
            Assert.AreEqual("fallback", _service.GetString("missing", "fallback"));
        }

        [Test]
        public void SetValue_And_GetString_RoundTrip()
        {
            _service.SetValue("name", "Alice");
            Assert.AreEqual("Alice", _service.GetString("name"));
        }

        [Test]
        public void GetInt_ParsesString()
        {
            _service.SetValue("count", "42");
            Assert.AreEqual(42, _service.GetInt("count"));
            Assert.AreEqual(0, _service.GetInt("missing"));
            Assert.AreEqual(10, _service.GetInt("missing", 10));
        }

        [Test]
        public void GetFloat_ParsesString()
        {
            _service.SetValue("rate", "3.14");
            Assert.AreEqual(3.14f, _service.GetFloat("rate"), 0.0001f);
        }

        [Test]
        public void GetBool_ParsesString()
        {
            _service.SetValue("flag", "true");
            Assert.IsTrue(_service.GetBool("flag"));
            _service.SetValue("flag", "false");
            Assert.IsFalse(_service.GetBool("flag"));
        }

        [Test]
        public void GetValue_ReturnsTypedObject()
        {
            _service.SetValue("i", "1");
            _service.SetValue("f", "1.5");
            _service.SetValue("b", "true");
            _service.SetValue("s", "hello");

            Assert.AreEqual(1, _service.GetValue("i"));
            Assert.AreEqual(1.5f, (float)_service.GetValue("f"), 0.0001f);
            Assert.AreEqual(true, _service.GetValue("b"));
            Assert.AreEqual("hello", _service.GetValue("s"));
        }

        [Test]
        public void GetValue_KeyWithDollar_TrimsDollar()
        {
            _service.SetValue("key", "value");
            Assert.AreEqual("value", _service.GetValue("$key"));
        }

        [Test]
        public void HasVariable_ReturnsCorrectly()
        {
            Assert.IsFalse(_service.HasVariable("x"));
            _service.SetValue("x", "1");
            Assert.IsTrue(_service.HasVariable("x"));
        }

        [Test]
        public void RemoveVariable_RemovesAndInvokesEvent()
        {
            _service.SetValue("a", "1");
            object oldVal = null, newVal = null;
            string keyReceived = null;
            _service.OnVariableChanged += (key, old, @new) =>
            {
                keyReceived = key;
                oldVal = old;
                newVal = @new;
            };
            _service.RemoveVariable("a");
            Assert.IsFalse(_service.HasVariable("a"));
            Assert.AreEqual("a", keyReceived);
            Assert.AreEqual("1", oldVal);
            Assert.IsNull(newVal);
        }

        [Test]
        public void Clear_RemovesAllVariables()
        {
            _service.SetValue("a", "1");
            _service.SetValue("b", "2");
            _service.Clear();
            Assert.IsFalse(_service.HasVariable("a"));
            Assert.IsFalse(_service.HasVariable("b"));
        }

        [Test]
        public void SetValue_InvokesOnVariableChanged()
        {
            string keyReceived = null;
            object oldReceived = null, newReceived = null;
            _service.OnVariableChanged += (key, old, @new) =>
            {
                keyReceived = key;
                oldReceived = old;
                newReceived = @new;
            };
            _service.SetValue("k", "v1");
            Assert.AreEqual("k", keyReceived);
            Assert.IsNull(oldReceived);
            Assert.AreEqual("v1", newReceived);

            _service.SetValue("k", "v2");
            Assert.AreEqual("v1", oldReceived);
            Assert.AreEqual("v2", newReceived);
        }
    }
}
