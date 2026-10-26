# Android 1.1.1

- Corrige o fechamento na abertura do APK Release (`UnsatisfiedLinkError` em `MainActivity.n_onCreate`).
- Desativa os marshal methods nativos e usa o registro JNI em tempo de execução.
- Isola os intermediários de publicação por código de versão para evitar reaproveitar wrappers de outra versão.
- Adiciona teste automatizado de instalação, abertura e reabertura do APK distribuído em Android 8 e Android 15.
- Mantém a assinatura anterior, permitindo atualizar sem desinstalar. O Windows permanece na versão 1.1.0.

# MDReader 1.1

- Windows atualizado para .NET 10 e WebView2.
- Modo escuro, fonte, zoom e largura de leitura configuráveis nas duas plataformas.
- Busca no documento, sumário clicável e atalho para o topo.
- Histórico, favoritos e recuperação da posição de leitura.
- Leitura assíncrona e bloqueio de HTML ativo no Windows e Android.
- Arrastar arquivos e registro opcional em Abrir com no Windows.
- Android: Abrir com, compartilhamento e seleção de pastas com imagens locais.
- Atualização automática quando o arquivo muda; no Android, a notificação depende do provedor de documentos e também é conferida ao voltar ao app.
- Geração automática de EXE/APK, checksums e atualização da pasta downloads por tags v*.

Windows: o executável inclui .NET; a visualização requer Microsoft Edge WebView2 Runtime. Android: versão 8.0 ou superior.
