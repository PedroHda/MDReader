# MDReader para Windows

**[Baixar MDReader.exe](https://github.com/PedroHda/MDReader/raw/refs/heads/master/downloads/windows/MDReader.exe)**

Versão **1.1.0**, Windows x64, portátil e com runtime .NET incluído. O arquivo pode ser colocado em qualquer pasta, sem depender de `bin` ou do código-fonte.

1. Baixe o executável e abra `MDReader.exe`.
2. Clique em **Abrir** e selecione um `.md` ou `.markdown`, ou arraste o arquivo para a janela.
3. Alterne entre **Visualização** e **Texto bruto**. Use busca, sumário, favoritos e os ajustes de leitura.

A visualização formatada requer o [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/). Sem ele, o modo de texto bruto permanece disponível. Não é necessário instalar o SDK .NET.

O executável não possui assinatura comercial de código. Confira a origem e o [SHA256SUMS](SHA256SUMS) antes de executá-lo. Metadados e tamanho: [version.json](version.json).

```powershell
Get-FileHash .\downloads\windows\MDReader.exe -Algorithm SHA256
```

Instruções de desenvolvimento, limitações, segurança e publicação: [README principal](../../README.md).
