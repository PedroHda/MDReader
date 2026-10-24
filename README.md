# MDReader

Leitor de Markdown para Windows, desenvolvido em **C# com Windows Forms**. Abra arquivos `.md` ou `.markdown`, consulte o texto original e visualize o conteúdo formatado em uma interface desktop.

A aplicação usa **Markdig** para converter Markdown em HTML e permite recarregar o documento depois de alterações feitas em outro editor.

## Índice

- [Funcionalidades](#funcionalidades)
- [Tecnologias](#tecnologias)
- [Requisitos](#requisitos)
- [Executar pelo código-fonte](#executar-pelo-código-fonte)
- [Como usar](#como-usar)
- [Gerar o executável portátil](#gerar-o-executável-portátil)
- [Distribuir o aplicativo](#distribuir-o-aplicativo)
- [Estrutura do projeto](#estrutura-do-projeto)
- [Renderização e limitações](#renderização-e-limitações)
- [Verificação e testes manuais](#verificação-e-testes-manuais)
- [Solução de problemas](#solução-de-problemas)
- [Contribuições](#contribuições)
- [Licença](#licença)

## Funcionalidades

- Seleção de arquivos pelo diálogo nativo do Windows.
- Aceitação de `.md` e `.markdown`, sem diferenciar letras maiúsculas de minúsculas.
- Exibição do nome do arquivo carregado.
- Aba **Texto bruto**, com o conteúdo original em modo somente leitura.
- Aba **Visualização**, com o Markdown convertido em HTML.
- Rolagem para leitura de documentos longos.
- Botão **Recarregar**, para atualizar o conteúdo do arquivo atual.
- Mensagens de sucesso, aviso e erro na interface.
- Tratamento de arquivos vazios, extensões não permitidas, caminhos inválidos, arquivos inexistentes e falhas de leitura.
- Ícone próprio na janela e no executável.
- Script para publicar um executável portátil para Windows 64 bits.

Entre as marcações processadas pelo Markdig estão títulos, negrito, itálico, tachado, listas, citações, links, imagens, tabelas, checklists, código inline, blocos de código e linhas horizontais.

## Tecnologias

| Tecnologia | Uso no projeto |
| --- | --- |
| C# / .NET | Linguagem e plataforma de execução |
| Windows Forms | Janela, botões, abas e seleção de arquivos |
| `net9.0-windows` | Framework de destino definido no projeto |
| Markdig `1.2.0` | Conversão de Markdown para HTML, com `UseAdvancedExtensions()` |
| `WebBrowser` do WinForms | Exibição do HTML na aba de visualização |
| PowerShell | Publicação do executável portátil |

A dependência NuGet está declarada em [MDReader.csproj](MDReader/MDReader.csproj) e é restaurada pelo .NET durante a compilação.

## Requisitos

### Para desenvolver ou gerar o executável

- Windows.
- SDK do .NET capaz de compilar o destino `net9.0-windows`, como o SDK .NET 9.
- Acesso à internet para restaurar os pacotes e componentes de publicação que ainda não estiverem no cache.
- PowerShell para executar o script de publicação.
- Git, caso queira clonar o repositório pela linha de comando.

Visual Studio é opcional. Também é possível trabalhar com o projeto pelo terminal.

### Para usar o executável portátil

- Windows 64 bits compatível com o runtime .NET incluído na publicação.
- O arquivo `MDReader.exe` gerado pelo script.

A publicação é **self-contained**: inclui o runtime necessário, portanto quem recebe essa versão não precisa instalar o SDK nem o runtime .NET separadamente.

## Executar pelo código-fonte

Clone o repositório e entre na pasta:

```powershell
git clone https://github.com/PedroHda/MDReader.git
cd MDReader
```

Se você já baixou o projeto, abra o PowerShell na pasta que contém `MDReader.sln`.

Confira o SDK disponível:

```powershell
dotnet --info
```

Restaure as dependências e inicie a aplicação:

```powershell
dotnet restore .\MDReader.sln
dotnet run --project .\MDReader\MDReader.csproj
```

Para compilar sem abrir a janela:

```powershell
dotnet build .\MDReader.sln -c Release
```

No Visual Studio, abra `MDReader.sln`, use o projeto `MDReader` como projeto de inicialização e execute com **F5** ou **Ctrl+F5**. O suporte a desenvolvimento desktop com .NET deve estar instalado.

## Como usar

1. Abra a aplicação.
2. Clique em **Selecionar Markdown**.
3. Escolha um arquivo `.md` ou `.markdown` no computador.
4. Consulte o conteúdo original na aba **Texto bruto**.
5. Abra a aba **Visualização** para ler o conteúdo formatado.
6. Depois de editar e salvar o documento em outro programa, clique em **Recarregar**.
7. Para trocar de documento, clique novamente em **Selecionar Markdown**.

O botão **Recarregar** fica disponível depois que um arquivo é carregado. Arquivos vazios ou contendo apenas espaços em branco geram um aviso, sem encerrar a aplicação.

O MDReader é um leitor: não edita nem salva alterações nos arquivos Markdown. A atualização do conteúdo é manual, pelo botão **Recarregar**.

## Gerar o executável portátil

Na raiz do repositório, execute pelo **PowerShell**:

```powershell
.\publish.ps1
```

Se a política de execução impedir o script, use este comando para executá-lo em um processo separado, sem alterar permanentemente a política do sistema:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\publish.ps1
```

Execute o comando no terminal. Dar dois cliques no arquivo `.ps1` pode abrir seu conteúdo em um editor, dependendo da associação de arquivos do Windows.

O resultado fica em:

```text
dist\MDReader.exe
```

O script publica em **Release** para **`win-x64`**, inclui o runtime .NET, combina os componentes em um arquivo único, habilita compressão e desabilita os símbolos de depuração na saída.

**A pasta `dist` é apagada e recriada a cada publicação.** Mantenha documentos pessoais e arquivos que deseja preservar fora dela.

Para abrir a pasta de saída:

```powershell
explorer .\dist
```

Também é possível gerar a mesma saída diretamente pelo .NET:

```powershell
dotnet publish .\MDReader\MDReader.csproj -c Release -r win-x64 --self-contained true -o .\dist /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:EnableCompressionInSingleFile=true /p:DebugType=None /p:DebugSymbols=false
```

## Distribuir o aplicativo

1. Gere uma publicação com `publish.ps1`.
2. Compacte o arquivo `dist\MDReader.exe` em um `.zip`, se o serviço de envio não aceitar executáveis.
3. Envie o arquivo ou anexe o pacote a uma Release do GitHub.
4. Quem receber deve extrair o `.zip`, quando houver, e abrir `MDReader.exe`.

O executável pode ser colocado em outra pasta do computador. Ele não depende de permanecer dentro de `bin` ou `dist`, nem exige o código-fonte, o PowerShell ou o Visual Studio para ser usado.

Imagens e outros recursos referenciados por um documento Markdown continuam sendo arquivos separados. Para compartilhar também o documento, preserve a organização dos seus recursos relativos.

As pastas `bin`, `obj` e `dist` são ignoradas pelo Git. Um push do código-fonte não publica automaticamente o executável; anexe-o a uma Release para disponibilizar o download pelo GitHub.

## Estrutura do projeto

```text
MDReader/
|-- .gitignore
|-- MDReader.sln
|-- README.md
|-- publish.ps1
|-- MDReader/
|   |-- MDReader.csproj
|   |-- Program.cs
|   |-- Assets/
|   |   `-- MD.ico
|   |-- Models/
|   |   `-- MarkdownDocument.cs
|   |-- Rendering/
|   |   `-- MarkdownPreviewRenderer.cs
|   |-- Services/
|   |   |-- MarkdownFileException.cs
|   |   |-- MarkdownFileReader.cs
|   |   `-- MarkdownFileValidator.cs
|   `-- Ui/
|       |-- MainForm.cs
|       `-- MainForm.resx
`-- dist/                         # Gerada pela publicação; ignorada pelo Git
```

| Arquivo | Responsabilidade |
| --- | --- |
| [Program.cs](MDReader/Program.cs) | Inicializa o Windows Forms e abre a janela principal |
| [MainForm.cs](MDReader/Ui/MainForm.cs) | Monta a interface, abre o seletor e apresenta documentos e mensagens |
| [MarkdownDocument.cs](MDReader/Models/MarkdownDocument.cs) | Representa o caminho, nome e conteúdo do documento e identifica conteúdo vazio |
| [MarkdownFileValidator.cs](MDReader/Services/MarkdownFileValidator.cs) | Valida o caminho, a existência do arquivo e a extensão |
| [MarkdownFileReader.cs](MDReader/Services/MarkdownFileReader.cs) | Lê o arquivo com `File.ReadAllText` e trata falhas de acesso e leitura |
| [MarkdownFileException.cs](MDReader/Services/MarkdownFileException.cs) | Define as exceções específicas de validação e leitura |
| [MarkdownPreviewRenderer.cs](MDReader/Rendering/MarkdownPreviewRenderer.cs) | Converte o Markdown em HTML e aplica os estilos da visualização |
| [publish.ps1](publish.ps1) | Gera o executável portátil na pasta `dist` |

O fluxo principal é: **seleção do arquivo > validação > leitura > documento > texto bruto e HTML renderizado**.

## Renderização e limitações

- A visualização usa o controle `WebBrowser` do WinForms, que utiliza o mecanismo legado de renderização do Windows. A aparência pode diferir de navegadores modernos e do GitHub.
- O diretório do arquivo Markdown é usado como base para resolver caminhos relativos, como `![Imagem](imagens/foto.png)`.
- A leitura de arquivos locais não exige conexão. Imagens e outros recursos com endereços remotos podem precisar de acesso à internet.
- Os blocos de código são formatados visualmente, mas não há destaque de sintaxe por linguagem integrado.
- Não há renderização integrada de diagramas Mermaid nem de fórmulas matemáticas com MathJax ou KaTeX.
- A conversão atual permite HTML embutido e não inclui uma etapa de sanitização. Abra documentos de fontes confiáveis.
- A leitura usa UTF-8 por padrão, com detecção de marca de codificação (BOM) pelo .NET. Não há seletor de codificação na interface.
- O arquivo inteiro é lido e renderizado de uma vez. Documentos muito grandes podem deixar a interface temporariamente ocupada.
- O script atual publica apenas para Windows 64 bits (`win-x64`). Não há instalador, associação automática de arquivos ou atualização automática implementados.

## Verificação e testes manuais

Para conferir a compilação e a formatação do código:

```powershell
dotnet build .\MDReader.sln
dotnet format .\MDReader.sln --verify-no-changes
```

O repositório ainda não possui uma suíte de testes automatizados. Use estes cenários para verificar o comportamento da aplicação:

| Cenário | Resultado esperado |
| --- | --- |
| Abrir um `.md` ou `.markdown` válido | Nome do arquivo, texto bruto, visualização e mensagem de sucesso |
| Abrir um arquivo com extensão `.MD` | Arquivo aceito normalmente |
| Selecionar outra extensão usando o filtro de todos os arquivos | Mensagem de extensão não permitida |
| Abrir um arquivo vazio ou contendo apenas espaços | Aviso de arquivo vazio; aplicação continua aberta |
| Cancelar o seletor | O documento atual permanece carregado |
| Editar e salvar o arquivo em outro editor; clicar em Recarregar | Conteúdo atualizado nas duas abas |
| Remover ou mover o arquivo; clicar em Recarregar | Mensagem amigável de arquivo inexistente |
| Abrir um arquivo sem permissão de leitura | Mensagem amigável de falha de acesso |
| Abrir um documento longo | Conteúdo acessível pela rolagem |
| Abrir o executável publicado em outra pasta | Aplicação inicia sem depender da pasta do projeto |

Para conferir a formatação, crie um arquivo `.md` com este conteúdo opcional:

````markdown
# Documento de teste

## Formatação

**Negrito**, *itálico*, ~~tachado~~ e `código inline`.

> Uma citação de exemplo.

- Primeiro item
- Segundo item
    - Subitem

1. Etapa um
2. Etapa dois

- [x] Documento aberto
- [ ] Conferir visualização

| Recurso | Situação |
| --- | --- |
| Texto bruto | Disponível |
| Visualização | Disponível |

```csharp
Console.WriteLine("Olá, Markdown!");
```

---
````

Esse arquivo é apenas uma sugestão de teste. A aplicação pode abrir qualquer arquivo com uma das extensões permitidas.

## Solução de problemas

| Problema | O que verificar |
| --- | --- |
| `dotnet` não é reconhecido | Instale o SDK .NET compatível e abra um novo terminal |
| O `.ps1` abre no Bloco de Notas | Execute o comando no PowerShell, conforme a seção de publicação |
| Scripts estão desabilitados | Use o comando com `-ExecutionPolicy Bypass` mostrado acima |
| O projeto ou script não é encontrado | Abra o terminal na raiz do repositório, onde está `MDReader.sln` |
| O executável não aparece em `dist` | Confira se o publish terminou com sucesso e revise os erros do terminal |
| A publicação não consegue substituir o executável | Feche a cópia do MDReader aberta a partir de `dist` e tente novamente |
| Extensão não permitida | Selecione um documento Markdown com extensão `.md` ou `.markdown` |
| Conteúdo desatualizado | Salve o documento no editor e clique em **Recarregar** |
| Acentos incorretos no documento | Salve o arquivo como UTF-8 no editor e recarregue |
| Imagem local não aparece | Verifique o caminho relativo à pasta do documento e a existência da imagem |
| Uma sintaxe aparece como texto ou com aparência diferente | Consulte as limitações de renderização e compare com a aba **Texto bruto** |

## Contribuições

Relate problemas nas [Issues do repositório](https://github.com/PedroHda/MDReader/issues), incluindo a mensagem exibida e um trecho mínimo de Markdown que reproduza o comportamento, sem dados pessoais.

Para contribuir com código, crie um branch, faça a alteração e execute as verificações de compilação e formatação antes de abrir um pull request. Descreva o comportamento esperado e como a mudança foi testada.

## Licença

O repositório ainda não contém um arquivo `LICENSE`. Uma licença de uso, modificação e redistribuição precisa ser definida pelo autor.
