using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ploeh.AutoFixture;
using StackDump;
using StackDump.Entities;

namespace StackDumpTests.IisWorkerProcessTests
{
    [TestClass]
    public class CreateFromTests
    {
        [TestMethod]
        public void IisWorkerProcess_CreateFrom()
        {
            var fixture = new Fixture();

            var workerProcessId = fixture.Create<int>();
            var applicationPoolName = fixture.Create("AppPool");

            var appCmdOutputLine = $@"WP ""{workerProcessId}"" (applicationPool:{applicationPoolName})";

            var result = IisWorkerProcess.CreateFrom(appCmdOutputLine);

            Assert.IsNotNull(result);
            Assert.AreEqual(workerProcessId, result.Id);
            Assert.AreEqual(applicationPoolName, result.AppPool);
        }
    }
}
