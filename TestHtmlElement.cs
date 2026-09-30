using System;
using System.Windows.Forms;

class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();

        using (var form = new Form())
        using (var wb = new WebBrowser())
        {
            wb.Dock = DockStyle.Fill;
            form.Controls.Add(wb);

            bool done = false;

            wb.DocumentCompleted += delegate
            {
                try
                {
                    Console.WriteLine("DocumentCompleted");
                    Console.WriteLine("Document null: " + (wb.Document == null));
                    Console.WriteLine(
                        "Body null: " +
                        (wb.Document == null || wb.Document.Body == null));

                    if (wb.Document != null && wb.Document.Body != null)
                    {
                        Console.WriteLine("TagName: " + wb.Document.Body.TagName);

                        wb.Document.Body.SetAttribute(
                            "contentEditable", "true");

                        Console.WriteLine("SetAttribute OK");
                        Console.WriteLine(
                            "contentEditable = " +
                            wb.Document.Body.GetAttribute("contentEditable"));

                        wb.Document.Body.InnerHtml =
                            "<b>Wine edit test</b>";

                        Console.WriteLine(
                            "InnerHtml = " + wb.Document.Body.InnerHtml);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("EXCEPTION:");
                    Console.WriteLine(ex);
                }

                done = true;
            };

            wb.Navigate("about:blank");

            while (!done)
            {
                Application.DoEvents();
                System.Threading.Thread.Sleep(10);
            }
        }

        Console.WriteLine("Fertig.");
    }
}
