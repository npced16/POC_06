using System;
using System.Collections.Generic;

using NUnit.Framework;

namespace IronRing.Interior.Tests
{
    /// <summary>Story 012 선로/내부 화면 전환 상태의 전이표.</summary>
    public sealed class InteriorViewModeTests
    {
        [Test]
        public void Open_SetsHubAndRaisesTrueOnce()
        {
            var mode = new InteriorViewMode();
            var events = new List<bool>();
            mode.InteriorChanged += events.Add;

            mode.Open("hub-1");
            mode.Open("hub-1");

            Assert.That(mode.InteriorOpen, Is.True);
            Assert.That(mode.InteriorHubId, Is.EqualTo("hub-1"));
            Assert.That(events, Is.EqualTo(new[] { true }));
        }

        [Test]
        public void Close_ClearsHubAndRaisesFalseOnce()
        {
            var mode = new InteriorViewMode();
            var events = new List<bool>();
            mode.Open("hub-1");
            mode.InteriorChanged += events.Add;

            mode.Close();
            mode.Close();

            Assert.That(mode.InteriorOpen, Is.False);
            Assert.That(mode.InteriorHubId, Is.Null);
            Assert.That(events, Is.EqualTo(new[] { false }));
        }

        [Test]
        public void Open_OtherHubWhileOpen_ClosesThenOpens()
        {
            var mode = new InteriorViewMode();
            var events = new List<string>();
            mode.InteriorChanged += open => events.Add((open ? "open:" : "close:") + (mode.InteriorHubId ?? "-"));
            mode.Open("hub-1");

            mode.Open("hub-2");

            Assert.That(events, Is.EqualTo(new[] { "open:hub-1", "close:-", "open:hub-2" }));
            Assert.That(mode.InteriorHubId, Is.EqualTo("hub-2"));
        }

        [Test]
        public void Open_BlankHub_IsRejected()
        {
            var mode = new InteriorViewMode();

            Assert.Throws<ArgumentException>(() => mode.Open(" "));
            Assert.Throws<ArgumentException>(() => mode.Open(null));
            Assert.That(mode.InteriorOpen, Is.False);
        }
    }
}
