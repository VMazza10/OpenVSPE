# OpenVSPE

Ponte Serial/TCP para balanças de porta serial (COM) e impressoras térmicas, feita em .NET 8.

Fiz o OpenVSPE porque o VSPE (Virtual Serial Ports Emulator) original exigia conta e não abria junto com o Windows. Aqui tudo roda como serviço do Windows: o computador liga e os canais já estão no ar, sem login. É um projeto independente, sem vínculo com a Eterlogic.

![Tela principal do OpenVSPE](docs/images/tela-principal.png)

## Download

Baixe o `.zip` da versão mais recente em [Releases](https://github.com/VMazza10/OpenVSPE/releases/latest), extraia e execute `OpenVSPE_V*.exe` como administrador.

## O que ele faz

Cada canal liga uma porta TCP a uma porta COM (para balanças) ou a uma impressora instalada no Windows, e dá para manter vários canais ao mesmo tempo. A interface segue o layout do VSPE, com barra de ferramentas, lista de canais e log.

Duas coisas facilitam a vida de quem instala no cliente. O botão "Reparar Balança" remove o dispositivo da porta COM e pede ao Windows para detectá-lo de novo. O instalador baixa o .NET 8 se ele estiver faltando, registra o serviço, libera o firewall e cria os atalhos.

O passo a passo de uso está no [manual](docs/Manual_OpenVSPE.md).

## Estrutura do repositório

| Pasta | Conteúdo |
| :--- | :--- |
| [/src](src) | Código-fonte da solução C# |
| [/scripts](scripts) | Scripts PowerShell de build, instalação e teste |
| [/docs](docs) | Manual do usuário |

A solução `OpenVSPE.sln` reúne os projetos de `src/`, inclusive o instalador:

| Projeto | Função |
| :--- | :--- |
| `OpenVSPE.Core` | Modelos, configuração (`channels.json`) e logs |
| `OpenVSPE.Serial` | Canais de porta serial |
| `OpenVSPE.Printing` | Canais de impressora térmica |
| `OpenVSPE.Service` | Serviço do Windows que gerencia os canais |
| `OpenVSPE.UI` | Interface WPF para configurar os canais |
| `OpenVSPE.Setup` | Instalador |

## Como compilar

Você precisa do SDK do .NET 8 e de um usuário administrador para instalar o serviço.

O script de build compila tudo e monta o pacote de instalação:
```powershell
cd scripts
.\build.ps1
```
A saída fica em `/build/releases/DEPLOY`. Os executáveis são gerados para x86, por compatibilidade com drivers de balança e de portas seriais virtuais.

Outros scripts em `scripts/`:
- `build.ps1` limpa, publica e empacota.
- `Instalar.ps1` ajuda a configurar o ambiente.
- `install-service.ps1` e `uninstall-service.ps1` instalam ou removem o serviço do Windows.
- `test-balanca.ps1`, `test-print.ps1` e `test-tcp.ps1` testam na mão a balança, a impressora e a conexão TCP.

## Licença

MIT. Veja [LICENSE](LICENSE).

Desenvolvido por Vinicius Mazaroski.
