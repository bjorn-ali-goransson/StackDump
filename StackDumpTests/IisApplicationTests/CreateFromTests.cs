using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using Ploeh.AutoFixture;
using StackDump;
using StackDump.Entities;

namespace StackDumpTests.IisApplicationTests
{
    [TestClass]
    public class CreateFromTests
    {
        [TestMethod]
        public void IisApplication_CreateFrom()
        {
            var fixture = new Fixture();

            var applicationName = fixture.Create("Application");
            var applicationPoolName = fixture.Create("AppPool");

            var appCmdOutputLine = $@"APP ""{applicationName}/"" (applicationPool:{applicationPoolName})";

            var result = IisApplication.CreateFrom(appCmdOutputLine);

            Assert.IsNotNull(result);
            Assert.AreEqual(applicationName, result.Name);
            Assert.AreEqual(applicationPoolName, result.AppPool);
        }
    }
}
