using System.IO;
using System.Text.RegularExpressions;

var file = "SRNSMudApp/Components/Item/AddItem.razor";
var content = File.ReadAllText(file);
content = content.Replace("[JSInvokable]\n    \n    public record MentionItem(string name, string replacement);\n\n    [JSInvokable]", "public record MentionItem(string name, string replacement);\n\n    [JSInvokable]");
File.WriteAllText(file, content);
