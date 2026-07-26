using System;
using System.Collections.Generic;
using Heddle.Data;
using Heddle.Language.Binding;
using Heddle.Runtime.Expressions;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Reflection-side driver of <see cref="PropLayoutCore"/>, paired with a generator-side twin to assert
    /// identical slot order and fault sequences across the two implementations.
    /// </summary>
    public class PropLayoutCoreReflectionTests
    {
        private sealed class RecordingSink : IPropLayoutSink<Type>
        {
            internal readonly List<PropFault> Faults = new List<PropFault>();
            internal readonly List<string> Names = new List<string>();
            internal readonly List<string> Messages = new List<string>();

            public void Fault(PropFault fault, PropDeclaration<Type> declaration, Type relatedType,
                string relatedDisplay)
            {
                Faults.Add(fault);
                Names.Add(declaration.Name);
                Messages.Add(HeddleDiagnosticCatalog.PropFaults.Message(fault, declaration.Name,
                    "extension 'probe'", ReflectionTypeFacts.Instance.Display(declaration.Type), relatedDisplay));
            }

            public bool TryConvertDefault(PropDeclaration<Type> declaration, Type targetType, out object converted,
                out string sourceDisplay)
            {
                sourceDisplay = declaration.DefaultValue?.GetType().Name ?? "null";
                return PropConversion.TryConvertLiteral(declaration.DefaultValue, declaration.DefaultValue == null,
                    targetType, out converted);
            }
        }

        private static PropDeclaration<Type> Decl(string name, Type type, int level = 0, bool hasDefault = false,
            object defaultValue = null) =>
            new PropDeclaration<Type>
            {
                Name = name, Type = type, Level = level, HasDefault = hasDefault, DefaultValue = defaultValue
            };

        [Fact]
        public void SlotIndicesFollowDeclarationOrderAcrossLayers()
        {
            var sink = new RecordingSink();
            var slots = PropLayoutCore.Build(new[]
            {
                Decl("a", typeof(int), level: 0),
                Decl("b", typeof(string), level: 0),
                Decl("c", typeof(object), level: 1),
            }, ReflectionTypeFacts.Instance, sink, out var faulted);

            Assert.False(faulted);
            Assert.Empty(sink.Faults);
            Assert.Equal(new[] { "a", "b", "c" }, slots.ConvertAll(s => s.Name));
            Assert.Equal(new[] { 0, 1, 2 }, slots.ConvertAll(s => s.Index));
        }

        [Fact]
        public void InheritedRedeclarationKeepsTheBaseSlotIndexAndReAppliesTheDefault()
        {
            var sink = new RecordingSink();
            var slots = PropLayoutCore.Build(new[]
            {
                Decl("a", typeof(object), level: 0, hasDefault: true, defaultValue: "base"),
                Decl("b", typeof(int), level: 0),
                Decl("a", typeof(string), level: 1, hasDefault: true, defaultValue: "derived"),
            }, ReflectionTypeFacts.Instance, sink, out var faulted);

            Assert.False(faulted);
            Assert.Equal(new[] { "a", "b" }, slots.ConvertAll(s => s.Name));
            Assert.Equal(0, slots[0].Index);            // kept, not appended
            Assert.Equal(typeof(string), slots[0].Type); // narrowed
            Assert.Equal("derived", slots[0].DefaultBoxed);
        }

        [Fact]
        public void FaultsAccumulateAndTheWalkContinues()
        {
            var sink = new RecordingSink();
            var slots = PropLayoutCore.Build(new[]
            {
                Decl("a", typeof(int)),
                Decl("a", typeof(int)),         // duplicate at level
                Decl("out", typeof(int)),       // reserved
                Decl(null, typeof(int)),        // name invalid
                Decl("b", typeof(int)),         // still resolved AFTER three faults
            }, ReflectionTypeFacts.Instance, sink, out var faulted);

            Assert.True(faulted);
            Assert.Equal(
                new[] { PropFault.DuplicateAtLevel, PropFault.NameReserved, PropFault.NameInvalid },
                sink.Faults);
            // The build tier used to stop at the first fault; both tiers now report all three, in declaration
            // order, and the walk still produces the slots it can.
            Assert.Equal(new[] { "a", "b" }, slots.ConvertAll(s => s.Name));
        }

        [Fact]
        public void ValidationOrderWithinOneDeclarationIsTheRuntimeOrder()
        {
            // A declaration that trips several checks at once reports only the FIRST in FaultOrder: a reserved
            // name that is also a duplicate and also carries an unusable type is NameReserved.
            var sink = new RecordingSink();
            PropLayoutCore.Build(new[]
            {
                Decl("out", typeof(int)),
                Decl("out", typeof(List<>)),
            }, ReflectionTypeFacts.Instance, sink, out _);

            Assert.Equal(new[] { PropFault.NameReserved, PropFault.NameReserved }, sink.Faults);
        }

        [Theory]
        [InlineData(typeof(List<>))]        // open generic (ContainsGenericParameters)
        [InlineData(null)]                  // null type
        public void UnusableTypesAreRejected(Type type)
        {
            var sink = new RecordingSink();
            PropLayoutCore.Build(new[] { Decl("a", type) }, ReflectionTypeFacts.Instance, sink, out var faulted);

            Assert.True(faulted);
            Assert.Equal(new[] { PropFault.TypeUnusable }, sink.Faults);
        }

        [Fact]
        public void ByRefAndPointerTypesAreRejected()
        {
            // By-ref and unbounded generics were missing from the generator's local predicate.
            var sink = new RecordingSink();
            PropLayoutCore.Build(new[]
            {
                Decl("a", typeof(int).MakeByRefType()),
                Decl("b", typeof(int).MakePointerType()),
                Decl("c", typeof(List<>).MakeGenericType(typeof(List<>).GetGenericArguments()[0])),
            }, ReflectionTypeFacts.Instance, sink, out var faulted);

            Assert.True(faulted);
            Assert.Equal(new[] { PropFault.TypeUnusable, PropFault.TypeUnusable, PropFault.TypeUnusable },
                sink.Faults);
        }

        [Fact]
        public void NonAssignableRedeclarationFaultsAndKeepsTheInheritedSlot()
        {
            var sink = new RecordingSink();
            var slots = PropLayoutCore.Build(new[]
            {
                Decl("a", typeof(string), level: 0),
                Decl("a", typeof(object), level: 1),
            }, ReflectionTypeFacts.Instance, sink, out var faulted);

            Assert.True(faulted);
            Assert.Equal(new[] { PropFault.RedeclarationNotAssignable }, sink.Faults);
            Assert.Equal(typeof(string), slots[0].Type);
            Assert.Equal(
                "Prop 'a' is re-declared with type System.Object, which is not assignable to the inherited type " +
                "System.String.", sink.Messages[0]);
        }

        [Fact]
        public void FaultOrderMatchesTheEnumDeclarationOrder()
        {
            // The ordered vocabulary is the contract; the enum's numeric order and PropFaults.FaultOrder must not
            // drift apart, because the core evaluates in FaultOrder and both tiers report by enum.
            var order = HeddleDiagnosticCatalog.PropFaults.FaultOrder;
            for (int i = 0; i < order.Length; i++)
                Assert.Equal(i, (int) order[i]);
            Assert.Equal(Enum.GetValues(typeof(PropFault)).Length, order.Length);
        }
    }
}
