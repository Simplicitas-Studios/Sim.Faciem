using System;
using NUnit.Framework;
using R3;

namespace Sim.Faciem.Tests
{
    public sealed class ViewModelDisposalTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void BaseAndInterfaceOwnersReleaseResourcesExactlyOnce(bool throughInterface)
        {
            var model = new OwnershipProbe();
            var resource = new CountingDisposable();
            model.Own(resource);
            BaseViewModel owner = model;

            if (throughInterface)
            {
                ((IDisposable)owner).Dispose();
                ((IDisposable)owner).Dispose();
            }
            else
            {
                owner.Dispose();
                owner.Dispose();
            }

            Assert.That(resource.Calls, Is.EqualTo(1));
        }

        [Test]
        public void InheritedGenericViewModelUsesTheSameOwnedBag()
        {
            var model = new GenericOwnershipProbe();
            var resource = new CountingDisposable();
            model.Own(resource);

            ((BaseViewModel)model).Dispose();

            Assert.That(resource.Calls, Is.EqualTo(1));
        }

        [Test]
        public void DisposableContainerRegistrationUsesTheOwnedBag()
        {
            var model = new OwnershipProbe();
            var resource = new CountingDisposable();
            ((IDisposableContainer)model).Add(resource);

            model.Dispose();

            Assert.That(resource.Calls, Is.EqualTo(1));
        }

        [Test]
        public void ResourcesAddedAfterDisposalAreDisposedImmediately()
        {
            var model = new OwnershipProbe();
            model.Dispose();
            var resource = new CountingDisposable();

            model.Own(resource);

            Assert.That(resource.Calls, Is.EqualTo(1));
        }

        [Test]
        public void EachViewModelOwnsAnIndependentBag()
        {
            var first = new OwnershipProbe();
            var second = new OwnershipProbe();
            var firstResource = new CountingDisposable();
            var secondResource = new CountingDisposable();
            first.Own(firstResource);
            second.Own(secondResource);

            first.Dispose();
            Assert.That(firstResource.Calls, Is.EqualTo(1));
            Assert.That(secondResource.Calls, Is.Zero);
            second.Dispose();
            Assert.That(secondResource.Calls, Is.EqualTo(1));
        }

        [Test]
        public void BagGrowthRetainsEveryOwnedResource()
        {
            var model = new OwnershipProbe();
            var resources = new CountingDisposable[12];
            for (int index = 0; index < resources.Length; index++)
            {
                resources[index] = new CountingDisposable();
                model.Own(resources[index]);
            }

            model.Dispose();

            foreach (CountingDisposable resource in resources)
            {
                Assert.That(resource.Calls, Is.EqualTo(1));
            }
        }

        [Test]
        public void BaseDisposalDetachesTheR3Subscription()
        {
            using (var source = new Subject<Unit>())
            {
                var model = new OwnershipProbe();
                int calls = 0;
                model.Own(source.Subscribe(_ => calls++));
                source.OnNext(Unit.Default);
                Assert.That(calls, Is.EqualTo(1));

                ((BaseViewModel)model).Dispose();
                source.OnNext(Unit.Default);

                Assert.That(calls, Is.EqualTo(1));
            }
        }

        private sealed class OwnershipProbe : BaseViewModel
        {
            public void Own(IDisposable resource)
            {
                Disposables.Add(resource);
            }
        }

        private sealed class GenericOwnershipProbe : ViewModel<GenericOwnershipProbe>
        {
            public void Own(IDisposable resource)
            {
                Disposables.Add(resource);
            }
        }

        private sealed class CountingDisposable : IDisposable
        {
            public int Calls { get; private set; }

            public void Dispose()
            {
                Calls++;
            }
        }
    }
}
