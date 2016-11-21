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
    public class ProxyMethodTests
    {
        [Test]
        public void can_call_a_void_method_via_proxy()
        {
            var target = new TargetSimplist();
            ISimplist proxy = Duck.Cast<ISimplist>(target);
            proxy.Execute();
            Assert.AreEqual(1, target.calls);
        }

        [Test]
        public void can_call_a_void_method_with_parameter_via_proxy()
        {
            var target = new TargetWithParameter();
            IWithNumber proxy = Duck.Cast<IWithNumber>(target);
            proxy.Execute(2);
            Assert.AreEqual(2, target.Number);
        }

        [Test]
        public void can_call_a_void_method_with_parameter_that_returns_something()
        {
            var target = new Adder();
            var proxy = Duck.Cast<IAdder>(target);
            Assert.AreEqual(3, proxy.AddOne(2));
        }

        [Test]
        public void cannot_cast_if_a_target_method_is_missing()
        {
            var target = new TargetBad();
            Assert.Throws<InvalidCastException>(() => Duck.Cast<ISimplist>(target));
        }

        [Test]
        public void explicit_cast_is_used_at_run_time()
        {
            var obj = (object)new AdderStatic();
            var i = (Adder)obj;
        }

        [Test]
        public void explicit_cast_is_used_at_compile_time()
        {
            var obj = new AdderStatic();
            var i = (Adder)obj;
        }
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
}
