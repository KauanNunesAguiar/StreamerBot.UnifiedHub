#if NET48
using System;
using System.Collections.Generic;
using System.Text;

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Polyfill exigido pelo compilador (C# 9+) para permitir records/propriedades
    /// "init" quando o alvo é netstandard2.0. Não existe em runtime nenhuma - é só
    /// um marcador que precisa existir durante a compilação.
    /// </summary>
    internal static class IsExternalInit { }
}
#endif