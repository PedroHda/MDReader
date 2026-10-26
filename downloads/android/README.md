# MDReader para Android

O instalador está nesta pasta: **[MDReader.apk](MDReader.apk)**.

**[Baixar APK diretamente](https://github.com/PedroHda/MDReader/raw/refs/heads/master/downloads/android/MDReader.apk)** · [Notas das versões](https://github.com/PedroHda/MDReader/releases)

| Informação | Valor |
| --- | --- |
| Versão | 1.1.0 (10100) |
| Sistema mínimo | Android 8.0 |
| Arquiteturas | ARM, ARM64 e x64 |
| Arquivo | `MDReader.apk` |
| Metadados e tamanho | [version.json](version.json) |

## Instalar

1. Baixe o APK pelo link acima ou pela opção de download na página do arquivo.
2. Envie-o ao celular e abra pelo gerenciador de arquivos.
3. Se solicitado, autorize esse gerenciador a instalar aplicativos dessa fonte.
4. Conclua a instalação e abra **MDReader**.
5. Toque em **Abrir arquivo** e selecione um `.md` ou `.markdown`.

O aplicativo inclui visualização formatada, texto bruto, busca, sumário, favoritos, histórico, modo escuro e ajustes de leitura. Use **Abrir pasta** para documentos com imagens relativas. Não exige instalação separada do .NET no celular.

## Integridade

O APK usa a mesma chave de assinatura da versão 1.0.0, permitindo instalação como atualização. O hash SHA-256 esperado está no arquivo [SHA256SUMS](SHA256SUMS), e os metadados estão em [version.json](version.json).

Para conferir no PowerShell, a partir da raiz do repositório:

```powershell
Get-FileHash .\downloads\android\MDReader.apk -Algorithm SHA256
```

As instruções de desenvolvimento, publicação e as limitações de cada plataforma estão no [README principal](../../README.md).
