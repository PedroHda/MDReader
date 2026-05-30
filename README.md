# MDReader

Aplicação desktop em C# Windows Forms para abrir, ler e visualizar arquivos Markdown (`.md` e `.markdown`).

## Tecnologia escolhida

O projeto não tinha uma estrutura de aplicação definida. Como o ambiente possui .NET disponível e a aplicação precisa de interface gráfica no Windows, a implementação foi feita com C# e Windows Forms.

A renderização Markdown usa a biblioteca NuGet `Markdig`, configurada no arquivo `MDReader/MDReader.csproj`.

## Funcionalidades

- Seleção de arquivos Markdown pelo computador.
- Validação de extensão `.md` e `.markdown`.
- Leitura do conteúdo do arquivo selecionado.
- Exibição do nome do arquivo carregado.
- Botão para recarregar o arquivo atual.
- Mensagens amigáveis para extensão inválida, arquivo inexistente, arquivo vazio e erro de leitura.
- Duas abas de visualização:
  - texto bruto Markdown;
  - visualização renderizada com suporte a sintaxes comuns do Markdown, como títulos, negrito, itálico, listas, links, imagens, tabelas, citações, blocos de código, código inline, tachado, regras horizontais e checklists.

## Como executar

1. Abra um terminal na pasta do projeto.
2. Execute:

```powershell
dotnet run --project .\MDReader\MDReader.csproj
```

## Como gerar o executável para enviar

Execute o script de publicação:

```powershell
.\publish.ps1
```

O arquivo final será gerado em:

```text
dist\MDReader.exe
```

Esse é o executável que você pode enviar para outra pessoa baixar e abrir no Windows, sem precisar mandar a pasta `bin`.

## Como testar manualmente

1. Clique em **Selecionar Markdown**.
2. Escolha um arquivo `.md` ou `.markdown`.
3. Verifique se o conteúdo aparece na aba **Texto bruto**.
4. Abra a aba **Visualização** para conferir a pré-visualização renderizada.
5. Use **Recarregar** depois de editar o arquivo externamente.
6. Tente selecionar um arquivo com outra extensão para validar a mensagem de erro.

## Build

```powershell
dotnet build .\MDReader.sln
```
