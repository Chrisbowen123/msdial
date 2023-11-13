using RDotNet;
using System.Windows;

namespace CompMs.App.Msdial.Model.Statistics
{
    public sealed class Notame
    {
        public void Run()
        {
            REngine.SetEnvironmentVariables();
            var engine = REngine.GetInstance();
            engine.Initialize();
            //engine.Evaluate("source('c:/src/myscript.r')");
            engine.Evaluate("x <- c(1, 2, 3, 4, 5)");
            engine.Evaluate("y <- c(10, 15, 13, 18, 20)");
            engine.Evaluate("plot(x, y, type='l')");

            engine.Evaluate("dev.copy(png, 'graph.png')");
            engine.Evaluate("dev.off()");

            MessageBox.Show("Graph generated and saved as 'graph.png'");
        }
    }
}