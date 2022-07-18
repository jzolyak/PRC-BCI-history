using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using ViewModels;

namespace ViewModelsTests
{
    [TestClass]
    public class MainMenuViewModelTests
    {
        private MockRepository mockRepository;

        private Mock<Action<string>> mockActionString;
        private Mock<Action> mockAction;

        [TestInitialize]
        public void TestInitialize()
        {
            this.mockRepository = new MockRepository(MockBehavior.Strict);

            this.mockActionString = this.mockRepository.Create<Action<string>>();
            this.mockAction = this.mockRepository.Create<Action>();
        }

        private MainMenuViewModel CreateViewModel()
        {
            return new MainMenuViewModel(
                this.mockActionString.Object,
                this.mockAction.Object);
        }

        [TestMethod]
        public void TestMethod1()
        {
            // Arrange
            var viewModel = this.CreateViewModel();

            // Act


            // Assert
            Assert.Fail();
            this.mockRepository.VerifyAll();
        }
    }
}
