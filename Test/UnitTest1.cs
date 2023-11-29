using Microsoft.VisualStudio.TestPlatform.TestHost;
using MockingbirdDotNet;
using System.Reflection;
using System;

namespace Test
{
    public interface IInterface
    {
        public DateTime Date { get; set; } // non nullable property. 
        public string ToString(string str);
        public int Sum(int x, int y);
    }

    class Mocks
    {
        public static int MockSum(int x, int y)
        {
            return x + y;
        }

        public static string MockToString(string str)
        {
            return str + " Mock";
        }
    }

    [TestClass]
    public class UnitTest1
    {
        string ToString(IInterface obj)
        {
            return obj.ToString("ToString");
        }

        int Sum(IInterface obj, int x, int y)
        {
            return obj.Sum(x, y);
        }

        [TestMethod]
        public void MockMethods()
        {
            var types = Mockingbird.Build(typeof(IInterface));
            var printDelegate = Delegate.CreateDelegate(types.MethodsDelegates[0], typeof(Mocks).GetMethod("MockToString"));
            var sumDelegate = Delegate.CreateDelegate(types.MethodsDelegates[1], typeof(Mocks).GetMethod("MockSum"));
            object obj = Activator.CreateInstance(types.InterfaceImplementation, printDelegate, sumDelegate);

            Assert.AreEqual(ToString((IInterface)obj), "ToString Mock" );
            Assert.AreEqual(Sum((IInterface)obj, 10, 10), 20);
        }

        [TestMethod]
        public void TestDateProperty()
        {
            var types = Mockingbird.Build(typeof(IInterface));
            object obj = Activator.CreateInstance(types.InterfaceImplementation);

            var dateProperty = types.InterfaceImplementation.GetProperty("Date");
            Assert.ThrowsException<TargetInvocationException>(() => dateProperty.GetValue(obj));
            Assert.ThrowsException<TargetInvocationException>(() => dateProperty.SetValue(obj, null));

            var date = new DateTime(2000, 1, 1);
            dateProperty.SetValue(obj, date);
            Assert.AreEqual(new DateTime(2000, 1, 1), ((IInterface)obj).Date);
        }
    }
}