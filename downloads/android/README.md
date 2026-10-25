# MDReader para Android

O instalador está nesta pasta: **[MDReader.apk](MDReader.apk)**.

**[Baixar APK diretamente](https://github.com/PedroHda/MDReader/raw/refs/heads/master/downloads/android/MDReader.apk)** · [Notas da versão 1.0.0](https://github.com/PedroHda/MDReader/releases/tag/android-v1.0.0)

| Informação | Valor |
| --- | --- |
| Versão | 1.0.0 |
| Sistema mínimo | Android 8.0 |
| Arquiteturas | ARM, ARM64 e x64 |
| Arquivo | `MDReader.apk` |
| Tamanho | 62.631.000 bytes |

## Instalar

1. Baixe o APK pelo link acima ou pela opção de download na página do arquivo.
2. Envie-o ao celular e abra pelo gerenciador de arquivos.
3. Se solicitado, autorize esse gerenciador a instalar aplicativos dessa fonte.
4. Conclua a instalação e abra **MDReader**.
5. Toque em **Abrir arquivo** e selecione um `.md` ou `.markdown`.

O aplicativo inclui visualização formatada, texto bruto e recarregamento do documento. Não exige instalação separada do .NET no celular.

## Integridade

Esta é a mesma versão assinada publicada na Release `android-v1.0.0`. O hash SHA-256 esperado está no arquivo [SHA256SUMS](SHA256SUMS).

Para conferir no PowerShell, a partir da raiz do repositório:

```powershell
Get-FileHash .\downloads\android\MDReader.apk -Algorithm SHA256
```

```text
51680f5fa826e811793e5008dc4121c8df7124b2221274a180963357d81bbed2
```

As instruções de desenvolvimento, publicação e as limitações de cada plataforma estão no [README principal](../../README.md).
