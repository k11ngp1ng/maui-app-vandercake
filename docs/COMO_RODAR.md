# Como rodar o VanderCake sozinho no Windows

## Sessao de testes ja validada nesta maquina (23/10/2026)
Para reabrir a mesma sessao usada com sucesso, clique em **Abrir-Teste.cmd** na pasta do projeto. Ele usa o banco e as credenciais privados ja existentes em artifacts/local-run/session.json, inicia/reutiliza o servidor HTTPS, verifica os dois logins e abre o site/aplicativo. As senhas aparecem na janela desse atalho e no arquivo privado ACCESS.md.

- Site desta sessao: https://localhost:7194/Admin.
- API no aplicativo Windows: https://localhost:7194.
- O aplicativo foi realmente autenticado e exibiu Bakery A/B/C; o navegador foi autenticado no Dashboard.
- A porta7080 pertence a outra instancia; ela nao foi interrompida ou reconfigurada. Nao misture o endereco/contas dessa instancia com a sessao7194.
- **Parar-Teste.cmd** encerra apenas o servidor cujo PID e linha de comando correspondem a esta sessao; preserva o banco. Feche o aplicativo separadamente.
- Abrir-Teste nao migra nem repoe dados. Para mudar o codigo, compile novamente apos encerrar os executaveis afetados.
- Os arquivos de sessao/senhas nao sao versionados. Em outra maquina/checkout, use a configuracao independente abaixo com suas proprias senhas.
- Verificacao real do atalho: Windows PowerShell5.1, reutilizacao de servidor ativo e autenticacao de ambas as contas. O atalho de encerramento nao foi executado, para deixar o sistema disponivel para o usuario.
## O que aconteceu nas imagens
A compilacao terminou com sucesso, e o certificado HTTPS foi confiado. Os erros seguintes sao de execucao:
- pwsh e o executavel do PowerShell7; o Windows PowerShell5.1 mostrado nas imagens nao o encontra no PATH. Para configurar o projeto, o Windows PowerShell existente e suficiente.
- Na segunda janela, o prompt estava em C:\Users\dirie. O caminho ./src/... foi procurado nessa pasta, nao na pasta do VanderCake. Cada aba tem sua propria pasta atual.

Nao e necessario instalar PowerShell7 para o procedimento abaixo. Scripts opcionais de verificacao/demo que usam APIs do PowerShell7 continuam com esse requisito; nao troque indiscriminadamente pwsh por powershell.exe nesses outros scripts.

## Opcao pratica: pelo Explorador de Arquivos
Abra a pasta C:\Users\dirie\OneDrive\Documentos\ChatGPT\DEV - VanderCake.

1. Na primeira configuracao, abra Configurar-Projeto.cmd. Ele restaura as ferramentas, compila, solicita confianca no certificado HTTPS e abre o script que pede duas senhas: administrador e vendedor. Aguarde concluir sem erro.
2. Abra Iniciar-Servidor.cmd. Deixe essa janela aberta enquanto testa.
3. Abra https://localhost:7080/Account/Login no navegador.
4. Abra Iniciar-App-Windows.cmd. Na tela de login do aplicativo, configure a API como https://localhost:7080.

No dia seguinte, normalmente basta repetir os passos2 a4. A compilacao so precisa ser refeita se o codigo mudar. Uma atualizacao de esquema exige migracao explicita; iniciar o servidor nao migra automaticamente.

Configurar-Projeto usa RemoteSigned apenas no processo filho do Windows PowerShell. Nao altera permanentemente a politica de execucao nem remove bloqueios de outros arquivos. Se uma politica da organizacao bloquear o script, a mensagem deve ser analisada; o atalho nao contorna Group Policy.

## Opcao pelo terminal: primeira configuracao
No Windows PowerShell, copie cada comando:
```powershell
Set-Location -LiteralPath "C:\Users\dirie\OneDrive\Documentos\ChatGPT\DEV - VanderCake"
dotnet tool restore
dotnet build VanderCake.slnx -c Debug
dotnet dev-certs https --trust
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File ".\scripts\Initialize-Development.ps1" -AcademicFixture
```
Escolha senhas com pelo menos12 caracteres, maiuscula, minuscula, numero e simbolo. Ao digitar a senha, ela fica oculta; isso e esperado. Guarde as duas senhas escolhidas. Nao existe senha padrao no codigo.

O script grava as senhas no user-secrets de desenvolvimento, aplica migracoes explicitamente e cria os dados iniciais se ainda nao existem. Se informar erro, nao continue supondo que a configuracao terminou.

## Iniciar o servidor
Na mesma janela:
```powershell
Set-Location -LiteralPath "C:\Users\dirie\OneDrive\Documentos\ChatGPT\DEV - VanderCake"
dotnet run --project src/VanderCake.Server --launch-profile https
```
Aguarde a mensagem de escuta em https://localhost:7080. Deixe o terminal aberto. O site e a API usam esse mesmo servidor.

Verificacao opcional em outra aba:
```powershell
Invoke-RestMethod "https://localhost:7080/health"
```
Esperado: status healthy e service VanderCake. Isso confirma que o processo responde, mas nao valida sozinho o banco/login.

## Abrir o aplicativo em OUTRA aba
Toda aba nova precisa entrar novamente na pasta do projeto:
```powershell
Set-Location -LiteralPath "C:\Users\dirie\OneDrive\Documentos\ChatGPT\DEV - VanderCake"
& ".\src\VanderCake.Mobile\bin\Debug\net10.0-windows10.0.19041.0\win-x64\VanderCake.Mobile.exe"
```
Tambem e possivel usar, de qualquer pasta:
```powershell
& "C:\Users\dirie\OneDrive\Documentos\ChatGPT\DEV - VanderCake\Iniciar-App-Windows.cmd"
```

## Logins e banco
- Site: admin@vandercake.local e a senha do administrador escolhida na configuracao.
- Aplicativo: salesperson@vandercake.local e a senha do vendedor escolhida na configuracao.
- API no Windows: https://localhost:7080. Se o aplicativo lembrar7194 de um teste antigo, altere para7080 antes de entrar.
- O site nao usa o login do vendedor; o aplicativo nao aceita o administrador.
- Banco padrao: %LOCALAPPDATA%\VanderCake\vandercake.db. Pedidos, vendas e pagamentos permanecem apos reiniciar.
- A sessao automatizada anterior usou outro arquivo, testing.db, e credenciais locais em artifacts/local-run/ACCESS.md. Essas credenciais NAO sao automaticamente as do banco padrao.
- ConnectionStrings__VanderCake, user-secrets ou appsettings.Local.json podem selecionar outro banco. Para verificar se ha uma variavel na aba atual: Get-Item Env:ConnectionStrings__VanderCake -ErrorAction SilentlyContinue. Use o mesmo banco na configuracao e no servidor.
- Repetir o seed nao altera senhas de contas existentes nem reabastece lotes consumidos. Trocar a senha informada no script nao redefine uma conta ja criada. Nao apague um banco para resolver erro de login.

## Problemas comuns e encerramento
- pwsh nao encontrado: use o comando powershell.exe indicado para Initialize-Development, ou o atalho Configurar-Projeto.cmd.
- Executavel nao encontrado: confira a pasta com Get-Location. O atalho Iniciar-App-Windows.cmd resolve o caminho pela propria localizacao.
- Porta7080/5080 em uso: ha outro servidor nessa porta. Pare a instancia anterior com Ctrl+C no terminal em que foi iniciada. Nao finalize processos desconhecidos.
- Janela do servidor fechou: a API/site pararam; execute Iniciar-Servidor.cmd novamente.
- Aplicativo nao conecta: mantenha o servidor aberto, use a URL HTTPS correta e verifique o certificado. Nao desative validacao TLS.
- Login invalido: confira papel, banco e senha original. Contas ja existentes preservam a senha anterior.
- Para parar: feche o aplicativo e pressione Ctrl+C no terminal do servidor. Isso nao apaga o banco.

## Android
Windows e site funcionam sem emulador. Para testar Android, abra o emulador e selecione o projeto MAUI/Android no Visual Studio2026 para compilar/instalar/depurar. O emulador usa https://10.0.2.2:7080; dispositivo fisico precisa de endereco HTTPS alcancavel na rede.

O certificado localhost nao e automaticamente confiado no Android: o Debug oferece um campo para o SHA256 exato do certificado local. O arquivo ACCESS.md anterior pode conter o pin daquela sessao, mas e preciso confirmar que o servidor atual usa o mesmo certificado. Nunca use excecao global de TLS. Veja MOBILE.md para os detalhes. Este guia nao declara um novo teste interativo Android.

