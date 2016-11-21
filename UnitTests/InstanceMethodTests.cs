using Ducks;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitTests
{
    [TestFixture]
    public class InstanceMethodTests
    {
        [Test]
        public void can_call_a_void_method_via_proxy()
        {
            var target = new TargetSimplist();
            ISimplist proxy = Instance.Cast<ISimplist>(target);
            proxy.Execute();
            Assert.AreEqual(1, target.calls);
        }

        [Test]
        public void can_call_a_void_method_with_parameter_via_proxy()
        {
            var target = new TargetWithParameter();
            IWithNumber proxy = Instance.Cast<IWithNumber>(target);
            proxy.Execute(2);
            Assert.AreEqual(2, target.Number);
        }

        [Test]
        public void can_call_a_void_method_with_parameter_that_returns_something()
        {
            var target = new Adder();
            var proxy = Instance.Cast<IAdder>(target);
            Assert.AreEqual(3, proxy.AddOne(2));
        }

        [Test]
        public void can_create_proxy_for_interface_that_inherits_other_interfaces()
        {
            var target = new Combined();
            var proxy = Instance.Cast<ISimplistWithAdder>(target);
            Assert.AreEqual(3, proxy.AddOne(2));
        }

        [Test]
        public void can_cast_proxy_to_another_interface_supported_by_wrapped_object()
        {
            var target = new Combined();
            var proxy = Instance.Cast<ISimplist>(target);
            Instance.Cast<IAdder>(proxy);
        }

        [Test]
        public void cannot_cast_if_a_target_method_is_missing()
        {
            var target = new TargetBad();
            Assert.Throws<InvalidCastException>(() => Instance.Cast<ISimplist>(target));
        }

        public interface ISimplistWithAdder : ISimplist, IAdder
        {
        }

        public interface ISimplist

        {
            void Execute();
        }

        public interface IWithNumber
        {
            void Execute(int num);
        }

        public interface IAdder
        {
            int AddOne(int num);
        }

        public class AdderStatic
        {
            public static explicit operator Adder(AdderStatic fred)
            {
                return new Adder();
            }
        }

        public class TargetSimplist
        {
            public int calls;

            public void Execute()
            {
                calls++;
            }
        }

        public class TargetWithParameter
        {
            public int Number;

            public void Execute(int num)
            {
                Number = num;
            }
        }

        public class Adder
        {
            public int AddOne(int num) => num + 1;
        }

        public class TargetBad
        {
            public void Fred2()
            {
            }
        }

        public class Combined
        {
            public int calls;

            public void Execute()
            {
                calls++;
            }

            public int AddOne(int num) => num + 1;
        }
    }

}