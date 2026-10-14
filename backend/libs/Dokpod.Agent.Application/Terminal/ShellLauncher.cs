using System.Text;
using System.Text.RegularExpressions;

namespace Dokpod.Agent.Application.Terminal;

/// <summary>
/// Comando fixo do terminal (spec 014 FR-002, D-003): um único exec escolhe `/bin/bash`, senão `/bin/sh`, e anuncia
/// o PID do shell (sequência OSC privada, removida da saída pelo agente) antes de `exec` no shell. O PID permite
/// encerrar o shell com SIGHUP, porque o engine não encerra execs ao fechar a conexão (D-007).
/// </summary>
public static partial class ShellLauncher
{
    public static readonly IReadOnlyList<string> Command =
    [
        "/bin/sh",
        "-c",
        "if [ -x /bin/bash ]; then S=/bin/bash; else S=/bin/sh; fi; printf '\\033]777;dokpod;%s;%s\\007' \"$$\" \"$S\"; exec \"$S\"",
    ];

    [GeneratedRegex("\u001b\\]777;dokpod;(\\d+);(/bin/(?:bash|sh))\u0007")]
    private static partial Regex Marker();

    /// <summary>Procura o marcador no início da saída; devolve PID, shell e o restante da saída sem o marcador.</summary>
    public static bool TryParse(string text, out int pid, out string shell, out string remainder)
    {
        var match = Marker().Match(text);
        if (!match.Success)
        {
            pid = 0;
            shell = string.Empty;
            remainder = text;
            return false;
        }

        pid = int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        shell = match.Groups[2].Value;
        remainder = text.Remove(match.Index, match.Length);
        return true;
    }

    public static byte[] Encode(string text) => Encoding.UTF8.GetBytes(text);
}
