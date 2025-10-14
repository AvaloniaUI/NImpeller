using System.Text;

namespace InteropGen;

class CodeGen
{
    private StringBuilder _sb = new StringBuilder();
    private int _indent;

    public struct IndentDisposable(CodeGen parent, string? closingLine, bool emptyLine = false)  :IDisposable
    {
        public void Dispose()
        {
            parent._indent--;
            if (closingLine != null)
                parent.Line(closingLine);
            if (emptyLine)
                parent.Line();
        }
    }

    public IndentDisposable Scope() => this.Line("{").Tab("}", true);
    
    public IndentDisposable Tab(string? closingLine = null, bool emptyLine = false)
    {
        _indent++;
        return new IndentDisposable(this, closingLine, emptyLine);
    }

    public CodeGen Line()
    {
        _sb.AppendLine();
        return this;
    }
    public CodeGen Line(string line)
    {
        _sb.Append(' ', _indent * 4);
        _sb.AppendLine(line);
        return this;
    }

    public override string ToString() => _sb.ToString();
}