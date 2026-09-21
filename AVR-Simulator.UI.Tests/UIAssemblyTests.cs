using System;
using System.IO;
using System.Windows;
using Xunit;

namespace AVR_Simulator.UI.Tests
{
    /// <summary>
    /// Smoke tests for the UI project.
    /// These tests verify logic that doesn't require a live WPF Dispatcher
    /// (no MainWindow is instantiated — that would require Application.Run).
    /// </summary>
    public class ConsoleManagerTests
    {
        // ── ConsoleManager ───────────────────────────────────────────────────

        [Fact]
        public void ConsoleManager_TypeExists()
        {
            // Confirms the UI assembly compiled and the type is accessible
            var t = typeof(ConsoleManager);
            Assert.NotNull(t);
            Assert.Equal("ConsoleManager", t.Name);
        }

        [Fact]
        public void ConsoleManager_IsInCorrectNamespace()
        {
            Assert.Equal("AVR_Simulator", typeof(ConsoleManager).Namespace);
        }
    }

    /// <summary>
    /// Checks that the namespace and expected WPF types are present in the UI assembly.
    /// </summary>
    public class AssemblySmoke
    {
        [Fact]
        public void MainWindow_TypeExists()
        {
            var t = typeof(MainWindow);
            Assert.NotNull(t);
        }

        [Fact]
        public void App_TypeExists()
        {
            var t = typeof(App);
            Assert.NotNull(t);
        }

        [Fact]
        public void MainWindow_IsWindow()
        {
            Assert.True(typeof(Window).IsAssignableFrom(typeof(MainWindow)),
                "MainWindow must derive from System.Windows.Window");
        }
    }
}
