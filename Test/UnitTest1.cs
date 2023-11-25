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
            var factory = new Mockingbird();
            var newType = factory.Build(typeof(IInterface));
            var printDelegate = Delegate.CreateDelegate(factory.DelegateTypes[0], typeof(Mocks).GetMethod("MockToString"));
            var sumDelegate = Delegate.CreateDelegate(factory.DelegateTypes[1], typeof(Mocks).GetMethod("MockSum"));
            object obj = Activator.CreateInstance(newType, printDelegate, sumDelegate);

            Assert.AreEqual(ToString((IInterface)obj), "ToString Mock" );
            Assert.AreEqual(Sum((IInterface)obj, 10, 10), 20);
        }

        [TestMethod]
        public void TestDateProperty()
        {
            var factory = new Mockingbird();
            var newType = factory.Build(typeof(IInterface));
            object obj = Activator.CreateInstance(newType);

            var dateProperty = newType.GetProperty("Date");
            Assert.ThrowsException<TargetInvocationException>(() => dateProperty.GetValue(obj));
            Assert.ThrowsException<TargetInvocationException>(() => dateProperty.SetValue(obj, null));

            var date = new DateTime(2000, 1, 1);
            dateProperty.SetValue(obj, date);
            Assert.AreEqual(new DateTime(2000, 1, 1), ((IInterface)obj).Date);
        }
    }
}