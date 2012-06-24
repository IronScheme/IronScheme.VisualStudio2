using System;
using System.IO;
using System.Text;
using IronScheme;
using IronScheme.Runtime;

namespace IronScheme.VisualStudio.REPL
{
  /// <summary>
  /// This class is the one responsible to build the command text that the engine will execute.
  /// The client of the class (the console window) can add a line of text using the Add method,
  /// then this class will check with the engine to see if the text built so far can be executed;
  /// if the text can be executed, then it will call the engine to run the command and will
  /// empty the buffer, otherwise it will add the text to the buffer and wait for the next
  /// Add to try to execute it.
  /// </summary>
  public class CommandBuffer
  {
    static readonly string singleLinePrompt = Resources.DefaultConsolePrompt;
    static readonly string multiLinePrompt = Resources.MultiLineConsolePrompt;

    StringBuilder textSoFar;
    Stream textBufferStream;

    public CommandBuffer(Stream textBufferStream)
    {
      this.textSoFar = new StringBuilder();
      this.textBufferStream = textBufferStream;

      // Write the command prompt.
      Write(singleLinePrompt);
    }

    public void Add(string text)
    {
      // This function is called to add a line, so write a new line delimeter to the output.
      Write(System.Environment.NewLine);

      // Add the text to the current buffer.
      if (textSoFar.Length > 0)
      {
        // We assume that Add is called to add a line, so if there is
        // previous text we have to create a new line.
        textSoFar.AppendLine();
      }
      textSoFar.Append(text);

      // Check with the engine if we can execute the text.
      bool allowIncomplete = !(string.IsNullOrEmpty(text) || (text.Trim().Length == 0));
      var expr = "(parse-repl {0})".Eval(textSoFar.ToString());
      bool canExecute = Builtins.IsTrue(expr);
      if (canExecute)
      {
        // If the text can be execute, then execute it and reset the text.
        try
        {
          try
          {
            var result = "(eval {0} (interaction-environment))".Eval(expr);
            if (!Builtins.IsTrue(Builtins.IsUnspecified(result)))
            {
              var fmt = "(format \"~s\" {0})".Eval<string>(result);
              Write(fmt + System.Environment.NewLine);
            }
          }
          catch (Exception ex)
          {
            Write(ex.ToString());
          }
        }
        finally
        {
          textSoFar.Length = 0;
          Write(singleLinePrompt);
        }
      }
      else
      {
        // If the command is not executed, then it is a multi-line command, so
        // we have to write the correct prompt to the output.
        Write(multiLinePrompt);
      }
    }

    public string Text
    {
      get { return textSoFar.ToString(); }
    }

    void Write(string text)
    {
      var writer = new System.IO.StreamWriter(textBufferStream);
      writer.Write(text);
      writer.Flush();
    }
  }
}