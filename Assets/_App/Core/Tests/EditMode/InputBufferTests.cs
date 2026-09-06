using ModularInput;
using NUnit.Framework;

namespace ProjectS.Core.Tests
{
    public sealed class InputBufferTests
    {
        private static InputBuffer<string> Create(
            int capacity = 4,
            InputBufferOverflowPolicy overflow = InputBufferOverflowPolicy.RejectIncoming,
            InputBufferSelectionPolicy selection = InputBufferSelectionPolicy.OldestFirst)
        {
            return new InputBuffer<string>(
                new InputBufferOptions(capacity, overflow, selection));
        }

        private static InputBufferWriteOptions Write(
            double lifetime = 1.0,
            int priority = 0,
            InputBufferDuplicatePolicy duplicates = InputBufferDuplicatePolicy.Allow)
        {
            return new InputBufferWriteOptions(lifetime, priority, duplicates);
        }

        [Test]
        public void StoredEntry_IsConsumedOnce()
        {
            InputBuffer<string> buffer = Create();
            Assert.IsTrue(buffer.TryEnqueue("attack", 0.0, Write()));

            Assert.IsTrue(buffer.TryConsume(0.1, out InputBufferEntry<string> entry));
            Assert.AreEqual("attack", entry.Action);
            Assert.IsFalse(buffer.TryConsume(0.1, out _));
            Assert.AreEqual(0, buffer.Count);
        }

        [Test]
        public void EntryExpiresAfterItsLifetime()
        {
            InputBuffer<string> buffer = Create();
            buffer.TryEnqueue("attack", 0.0, Write(lifetime: 0.2));

            Assert.IsFalse(buffer.TryConsume(0.5, out _));
            Assert.AreEqual(0, buffer.Count);
        }

        [Test]
        public void EntrySurvivesUntilItsLifetimeElapses()
        {
            InputBuffer<string> buffer = Create();
            buffer.TryEnqueue("attack", 0.0, Write(lifetime: 0.2));

            Assert.IsTrue(buffer.TryConsume(0.19, out _));
        }

        [Test]
        public void OldestFirstSelection_ReturnsInsertionOrder()
        {
            InputBuffer<string> buffer = Create();
            buffer.TryEnqueue("first", 0.0, Write());
            buffer.TryEnqueue("second", 0.1, Write());

            buffer.TryConsume(0.2, out InputBufferEntry<string> entry);

            Assert.AreEqual("first", entry.Action);
        }

        [Test]
        public void NewestFirstSelection_ReturnsMostRecent()
        {
            InputBuffer<string> buffer = Create(
                selection: InputBufferSelectionPolicy.NewestFirst);
            buffer.TryEnqueue("first", 0.0, Write());
            buffer.TryEnqueue("second", 0.1, Write());

            buffer.TryConsume(0.2, out InputBufferEntry<string> entry);

            Assert.AreEqual("second", entry.Action);
        }

        [Test]
        public void PredicateConsume_SkipsNonMatchingEntries()
        {
            InputBuffer<string> buffer = Create();
            buffer.TryEnqueue("dodge", 0.0, Write());
            buffer.TryEnqueue("attack", 0.1, Write());

            bool consumed = buffer.TryConsume(
                0.2,
                (in InputBufferEntry<string> candidate) => candidate.Action == "attack",
                out InputBufferEntry<string> entry);

            Assert.IsTrue(consumed);
            Assert.AreEqual("attack", entry.Action);
            Assert.AreEqual(1, buffer.Count);
        }

        [Test]
        public void RejectIncomingOverflow_KeepsExistingEntries()
        {
            InputBuffer<string> buffer = Create(capacity: 2);
            buffer.TryEnqueue("a", 0.0, Write());
            buffer.TryEnqueue("b", 0.0, Write());

            Assert.IsFalse(buffer.TryEnqueue("c", 0.0, Write()));
            Assert.AreEqual(2, buffer.Count);
        }

        [Test]
        public void DropOldestOverflow_MakesRoomForNewest()
        {
            InputBuffer<string> buffer = Create(
                capacity: 2,
                overflow: InputBufferOverflowPolicy.DropOldest);
            buffer.TryEnqueue("a", 0.0, Write());
            buffer.TryEnqueue("b", 0.1, Write());

            Assert.IsTrue(buffer.TryEnqueue("c", 0.2, Write()));
            Assert.AreEqual(2, buffer.Count);
            buffer.TryConsume(0.3, out InputBufferEntry<string> entry);
            Assert.AreEqual("b", entry.Action);
        }

        [Test]
        public void RejectDuplicates_KeepsTheOriginalEntry()
        {
            InputBuffer<string> buffer = Create();
            buffer.TryEnqueue("attack", 0.0, Write(lifetime: 1.0));

            bool stored = buffer.TryEnqueue(
                "attack",
                0.1,
                Write(duplicates: InputBufferDuplicatePolicy.RejectIncoming));

            Assert.IsFalse(stored);
            Assert.AreEqual(1, buffer.Count);
        }

        [Test]
        public void PruneExpired_RemovesOnlyStaleEntries()
        {
            InputBuffer<string> buffer = Create();
            buffer.TryEnqueue("short", 0.0, Write(lifetime: 0.1));
            buffer.TryEnqueue("long", 0.0, Write(lifetime: 5.0));

            int pruned = buffer.PruneExpired(0.5);

            Assert.AreEqual(1, pruned);
            Assert.AreEqual(1, buffer.Count);
            buffer.TryPeek(0.5, out InputBufferEntry<string> entry);
            Assert.AreEqual("long", entry.Action);
        }

        [Test]
        public void Clear_EmptiesTheBuffer()
        {
            InputBuffer<string> buffer = Create();
            buffer.TryEnqueue("a", 0.0, Write());
            buffer.TryEnqueue("b", 0.0, Write());

            buffer.Clear();

            Assert.AreEqual(0, buffer.Count);
            Assert.IsFalse(buffer.TryPeek(0.0, out _));
        }

        [Test]
        public void Peek_DoesNotRemoveTheEntry()
        {
            InputBuffer<string> buffer = Create();
            buffer.TryEnqueue("attack", 0.0, Write());

            Assert.IsTrue(buffer.TryPeek(0.1, out _));
            Assert.AreEqual(1, buffer.Count);
        }
    }
}
