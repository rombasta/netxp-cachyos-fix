using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

class FixWineInterface
{
    static void ReplaceBodyCalls(TypeDefinition editor, TypeDefinition htmlElement)
    {
        string[] names = {
            "get_innerText", "set_innerText",
            "get_innerHTML", "set_innerHTML",
            "get_outerHTML", "set_outerHTML"
        };

        foreach (var method in editor.Methods.Where(m => m.HasBody))
        foreach (var ins in method.Body.Instructions)
        {
            var old = ins.Operand as MethodReference;
            if (old == null ||
                old.DeclaringType.FullName != "mshtml.DispHTMLBody" ||
                !names.Contains(old.Name))
                continue;

            var replacement = htmlElement.Methods.FirstOrDefault(x =>
                x.Name == old.Name &&
                x.Parameters.Count == old.Parameters.Count);

            if (replacement == null)
                throw new Exception("IHTMLElement-Methode fehlt: " + old.Name);

            Console.WriteLine(method.Name + ": " + old.Name +
                              " -> IHTMLElement." + replacement.Name);
            ins.Operand = replacement;
        }
    }

    static void RemoveHtmlBodyCasts(TypeDefinition editor)
    {
        foreach (var method in editor.Methods.Where(m => m.HasBody))
        {
            var il = method.Body.GetILProcessor();
            var casts = method.Body.Instructions
                .Where(i => i.OpCode == OpCodes.Castclass &&
                    i.Operand is TypeReference tr &&
                    tr.FullName == "mshtml.HTMLBody")
                .ToList();

            foreach (var cast in casts)
            {
                Console.WriteLine("Entferne HTMLBody-Cast in " + method.Name);
                il.Remove(cast);
            }
        }
    }

    static void RebuildReadOnly(TypeDefinition editor, ModuleDefinition module)
    {
        var method = editor.Methods.First(m =>
            m.Name == "set_ReadOnly" && m.Parameters.Count == 1);

        var readOnly = editor.Fields.First(f => f.Name == "_readOnly");
        var toolstrip = editor.Fields.First(f => f.Name == "toolstripEditor");
        var browser = editor.Fields.First(f => f.Name == "editorWebBrowser");

        method.Body.ExceptionHandlers.Clear();
        method.Body.Variables.Clear();
        method.Body.Instructions.Clear();
        method.Body.InitLocals = false;
        method.Body.MaxStackSize = 4;

        var il = method.Body.GetILProcessor();

        var getDocument = module.ImportReference(
            typeof(System.Windows.Forms.WebBrowser).GetProperty("Document").GetGetMethod());
        var getBody = module.ImportReference(
            typeof(System.Windows.Forms.HtmlDocument).GetProperty("Body").GetGetMethod());
        var setAttribute = module.ImportReference(
            typeof(System.Windows.Forms.HtmlElement).GetMethod(
                "SetAttribute", new Type[] { typeof(string), typeof(string) }));
        var setEnabled = module.ImportReference(
            typeof(System.Windows.Forms.Control).GetProperty("Enabled").GetSetMethod());
        var setContext = module.ImportReference(
            typeof(System.Windows.Forms.WebBrowser)
                .GetProperty("IsWebBrowserContextMenuEnabled").GetSetMethod());

        il.Append(il.Create(OpCodes.Ldarg_0));
        il.Append(il.Create(OpCodes.Ldarg_1));
        il.Append(il.Create(OpCodes.Stfld, readOnly));

        var afterEditable = il.Create(OpCodes.Nop);

        il.Append(il.Create(OpCodes.Ldarg_0));
        il.Append(il.Create(OpCodes.Ldfld, browser));
        il.Append(il.Create(OpCodes.Callvirt, getDocument));
        il.Append(il.Create(OpCodes.Brfalse, afterEditable));

        il.Append(il.Create(OpCodes.Ldarg_0));
        il.Append(il.Create(OpCodes.Ldfld, browser));
        il.Append(il.Create(OpCodes.Callvirt, getDocument));
        il.Append(il.Create(OpCodes.Callvirt, getBody));
        il.Append(il.Create(OpCodes.Brfalse, afterEditable));

        il.Append(il.Create(OpCodes.Ldarg_0));
        il.Append(il.Create(OpCodes.Ldfld, browser));
        il.Append(il.Create(OpCodes.Callvirt, getDocument));
        il.Append(il.Create(OpCodes.Callvirt, getBody));
        il.Append(il.Create(OpCodes.Ldstr, "contentEditable"));
        il.Append(il.Create(OpCodes.Ldarg_0));
        il.Append(il.Create(OpCodes.Ldfld, readOnly));

        var editableFalse = il.Create(OpCodes.Ldstr, "false");
        var editableDone = il.Create(OpCodes.Nop);

        il.Append(il.Create(OpCodes.Brtrue, editableFalse));
        il.Append(il.Create(OpCodes.Ldstr, "true"));
        il.Append(il.Create(OpCodes.Br, editableDone));
        il.Append(editableFalse);
        il.Append(editableDone);
        il.Append(il.Create(OpCodes.Callvirt, setAttribute));
        il.Append(afterEditable);

        il.Append(il.Create(OpCodes.Ldarg_0));
        il.Append(il.Create(OpCodes.Ldfld, toolstrip));
        il.Append(il.Create(OpCodes.Ldarg_0));
        il.Append(il.Create(OpCodes.Ldfld, readOnly));
        il.Append(il.Create(OpCodes.Ldc_I4_0));
        il.Append(il.Create(OpCodes.Ceq));
        il.Append(il.Create(OpCodes.Callvirt, setEnabled));

        il.Append(il.Create(OpCodes.Ldarg_0));
        il.Append(il.Create(OpCodes.Ldfld, browser));
        il.Append(il.Create(OpCodes.Ldarg_0));
        il.Append(il.Create(OpCodes.Ldfld, readOnly));
        il.Append(il.Create(OpCodes.Callvirt, setContext));
        il.Append(il.Create(OpCodes.Ret));

        Console.WriteLine(
            "set_ReadOnly: contentEditable ueber WinForms HtmlElement.SetAttribute");
    }

    static void RebuildScrollBars(TypeDefinition editor, ModuleDefinition module)
    {
        var method = editor.Methods.First(m =>
            m.Name == "set_ScrollBars" && m.Parameters.Count == 1);
        var scrollBars = editor.Fields.First(f => f.Name == "_scrollBars");
        var menu = editor.Fields.First(f => f.Name == "contextDocumentScrollbar");

        method.Body.ExceptionHandlers.Clear();
        method.Body.Variables.Clear();
        method.Body.Instructions.Clear();
        method.Body.InitLocals = false;
        method.Body.MaxStackSize = 3;

        var il = method.Body.GetILProcessor();

        il.Append(il.Create(OpCodes.Ldarg_0));
        il.Append(il.Create(OpCodes.Ldarg_1));
        il.Append(il.Create(OpCodes.Stfld, scrollBars));

        il.Append(il.Create(OpCodes.Ldarg_0));
        il.Append(il.Create(OpCodes.Ldfld, menu));
        il.Append(il.Create(OpCodes.Ldarg_1));
        il.Append(il.Create(OpCodes.Ldc_I4_1));

        var notOne = il.Create(OpCodes.Ldc_I4_1);
        var done = il.Create(OpCodes.Nop);

        il.Append(il.Create(OpCodes.Bne_Un_S, notOne));
        il.Append(il.Create(OpCodes.Ldc_I4_0));
        il.Append(il.Create(OpCodes.Br_S, done));
        il.Append(notOne);
        il.Append(done);

        var setChecked = module.ImportReference(
            typeof(System.Windows.Forms.ToolStripMenuItem)
                .GetProperty("Checked").GetSetMethod());

        il.Append(il.Create(OpCodes.Callvirt, setChecked));
        il.Append(il.Create(OpCodes.Ret));

        Console.WriteLine("set_ScrollBars: DispHTMLBody-Zugriff entfernt");
    }

    static void RebuildWordWrap(TypeDefinition editor, ModuleDefinition module)
    {
        var method = editor.Methods.First(m =>
            m.Name == "set_AutoWordWrap" && m.Parameters.Count == 1);
        var wordWrap = editor.Fields.First(f => f.Name == "_autoWordWrap");
        var menu = editor.Fields.First(f => f.Name == "contextDocumentWordwrap");

        method.Body.ExceptionHandlers.Clear();
        method.Body.Variables.Clear();
        method.Body.Instructions.Clear();
        method.Body.InitLocals = false;
        method.Body.MaxStackSize = 2;

        var il = method.Body.GetILProcessor();

        il.Append(il.Create(OpCodes.Ldarg_0));
        il.Append(il.Create(OpCodes.Ldarg_1));
        il.Append(il.Create(OpCodes.Stfld, wordWrap));

        il.Append(il.Create(OpCodes.Ldarg_0));
        il.Append(il.Create(OpCodes.Ldfld, menu));
        il.Append(il.Create(OpCodes.Ldarg_1));

        var setChecked = module.ImportReference(
            typeof(System.Windows.Forms.ToolStripMenuItem)
                .GetProperty("Checked").GetSetMethod());

        il.Append(il.Create(OpCodes.Callvirt, setChecked));
        il.Append(il.Create(OpCodes.Ret));

        Console.WriteLine("set_AutoWordWrap: DispHTMLBody-Zugriff entfernt");
    }

    static void RebuildBodyHtml(
        TypeDefinition editor,
        TypeDefinition htmlElement,
        ModuleDefinition module)
    {
        var method = editor.Methods.First(m =>
            m.Name == "set_BodyHtml" && m.Parameters.Count == 1);
        var body = editor.Fields.First(f => f.Name == "body");
        var bodyUrl = editor.Fields.First(f => f.Name == "_bodyUrl");
        var bodyText = editor.Fields.First(f => f.Name == "_bodyText");
        var bodyHtml = editor.Fields.First(f => f.Name == "_bodyHtml");

        var setInnerHtml = htmlElement.Methods.First(m =>
            m.Name == "set_innerHTML" && m.Parameters.Count == 1);
        var getInnerHtml = htmlElement.Methods.First(m =>
            m.Name == "get_innerHTML" && m.Parameters.Count == 0);
        var getInnerText = htmlElement.Methods.First(m =>
            m.Name == "get_innerText" && m.Parameters.Count == 0);

        method.Body.ExceptionHandlers.Clear();
        method.Body.Variables.Clear();
        method.Body.Instructions.Clear();
        method.Body.InitLocals = false;
        method.Body.MaxStackSize = 3;

        var il = method.Body.GetILProcessor();
        var stringEmpty = module.ImportReference(typeof(string).GetField("Empty"));

        il.Append(il.Create(OpCodes.Ldarg_0));
        il.Append(il.Create(OpCodes.Ldsfld, stringEmpty));
        il.Append(il.Create(OpCodes.Stfld, bodyUrl));

        var haveValue = il.Create(OpCodes.Nop);
        il.Append(il.Create(OpCodes.Ldarg_1));
        il.Append(il.Create(OpCodes.Brtrue_S, haveValue));
        il.Append(il.Create(OpCodes.Ldsfld, stringEmpty));
        il.Append(il.Create(OpCodes.Starg_S, method.Parameters[0]));
        il.Append(haveValue);

        il.Append(il.Create(OpCodes.Ldarg_0));
        il.Append(il.Create(OpCodes.Ldfld, body));
        il.Append(il.Create(OpCodes.Ldarg_1));
        il.Append(il.Create(OpCodes.Callvirt, setInnerHtml));

        il.Append(il.Create(OpCodes.Ldarg_0));
        il.Append(il.Create(OpCodes.Ldarg_0));
        il.Append(il.Create(OpCodes.Ldfld, body));
        il.Append(il.Create(OpCodes.Callvirt, getInnerText));
        il.Append(il.Create(OpCodes.Stfld, bodyText));

        il.Append(il.Create(OpCodes.Ldarg_0));
        il.Append(il.Create(OpCodes.Ldarg_0));
        il.Append(il.Create(OpCodes.Ldfld, body));
        il.Append(il.Create(OpCodes.Callvirt, getInnerHtml));
        il.Append(il.Create(OpCodes.Stfld, bodyHtml));

        il.Append(il.Create(OpCodes.Ret));

        Console.WriteLine("set_BodyHtml: portable IHTMLElement-Version");
    }

    static void RebuildGetBodyHtml(
        TypeDefinition editor,
        TypeDefinition htmlElement,
        ModuleDefinition module)
    {
        var method = editor.Methods.First(m =>
            m.Name == "get_BodyHtml" && m.Parameters.Count == 0);
        var body = editor.Fields.First(f => f.Name == "body");
        var getOuterHtml = htmlElement.Methods.First(m =>
            m.Name == "get_outerHTML" && m.Parameters.Count == 0);
        var trim = module.ImportReference(
            typeof(string).GetMethod("Trim", Type.EmptyTypes));

        method.Body.ExceptionHandlers.Clear();
        method.Body.Variables.Clear();
        method.Body.Instructions.Clear();
        method.Body.InitLocals = false;
        method.Body.MaxStackSize = 1;

        var il = method.Body.GetILProcessor();

        il.Append(il.Create(OpCodes.Ldarg_0));
        il.Append(il.Create(OpCodes.Ldfld, body));
        il.Append(il.Create(OpCodes.Callvirt, getOuterHtml));
        il.Append(il.Create(OpCodes.Callvirt, trim));
        il.Append(il.Create(OpCodes.Ret));

        Console.WriteLine("get_BodyHtml: IHTMLElement.get_outerHTML");
    }

    static void PatchRebaseAnchorUrl(TypeDefinition editor)
    {
        var method = editor.Methods.First(m =>
            m.Name == "RebaseAnchorUrl" && m.Parameters.Count == 0);
        var document = editor.Fields.First(f => f.Name == "document");
        var instructions = method.Body.Instructions.ToList();

        var oldCall = instructions.First(i =>
            i.OpCode == OpCodes.Callvirt &&
            i.Operand is MethodReference mr &&
            mr.DeclaringType.FullName == "mshtml.DispHTMLBody" &&
            mr.Name == "getElementsByTagName");

        var documentType = editor.Module.Types.First(t =>
            t.FullName == "mshtml.DispHTMLDocument");
        var newCall = documentType.Methods.First(m =>
            m.Name == "getElementsByTagName" && m.Parameters.Count == 1);

        var bodyLoad = oldCall.Previous;
        while (bodyLoad != null)
        {
            if (bodyLoad.OpCode == OpCodes.Ldfld &&
                bodyLoad.Operand is FieldReference fr &&
                fr.Name == "body")
                break;
            bodyLoad = bodyLoad.Previous;
        }

        if (bodyLoad == null)
            throw new Exception("body-load in RebaseAnchorUrl nicht gefunden");

        bodyLoad.Operand = document;
        oldCall.Operand = newCall;

        Console.WriteLine(
            "RebaseAnchorUrl: body.getElementsByTagName -> " +
            "document.getElementsByTagName");
    }

    static void Main()
    {
        const string input = "MSDN.HtmlEditorControl.dll.original";
        const string output = "MSDN.HtmlEditorControl.dll.wineinterface2";

        var asm = AssemblyDefinition.ReadAssembly(input);
        var module = asm.MainModule;
        var editor = module.Types.First(t =>
            t.FullName == "MSDN.Html.Editor.HtmlEditorControl");
        var htmlElement = module.Types.First(t =>
            t.FullName == "mshtml.IHTMLElement");
        var body = editor.Fields.First(f => f.Name == "body");

        Console.WriteLine(
            "body: " + body.FieldType.FullName + " -> " + htmlElement.FullName);

        body.FieldType = htmlElement;

        foreach (var method in editor.Methods.Where(m => m.HasBody))
        foreach (var ins in method.Body.Instructions)
        {
            if ((ins.OpCode == OpCodes.Ldfld ||
                 ins.OpCode == OpCodes.Stfld ||
                 ins.OpCode == OpCodes.Ldflda) &&
                ins.Operand is FieldReference fr &&
                fr.Name == "body" &&
                fr.DeclaringType.FullName == editor.FullName)
                fr.FieldType = htmlElement;
        }

        RemoveHtmlBodyCasts(editor);
        ReplaceBodyCalls(editor, htmlElement);
        RebuildReadOnly(editor, module);
        RebuildScrollBars(editor, module);
        RebuildWordWrap(editor, module);
        RebuildBodyHtml(editor, htmlElement, module);
        RebuildGetBodyHtml(editor, htmlElement, module);
        PatchRebaseAnchorUrl(editor);

        asm.Write(output);
        Console.WriteLine("Geschrieben: " + output);
    }
}
