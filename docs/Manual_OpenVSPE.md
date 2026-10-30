# Manual do Usuário e Visão Geral - OpenVSPE

Bem-vindo ao **OpenVSPE**, a ponte definitiva para comunicação entre sistemas de PDV (Ponto de Venda) e suas impressoras/balanças!

---

## 1. O que o OpenVSPE faz? (Visão Geral)

O OpenVSPE funciona como um "tradutor" e "ponte" de conexões. Muitos sistemas de Ponto de Venda (PDVs) antigos ou específicos só conseguem enviar impressões através da rede (IP) ou ler peso de balanças através de portas seriais (COM). 
O OpenVSPE conecta essas duas pontas de forma invisível e automática!

### Principais Recursos e Vantagens:
✅ **Impressão Direta no Cabo USB via Rede (Printer RAW):** 
Recebe comandos de impressão do seu PDV (enviados para um IP e Porta) e "cospe" diretamente na impressora conectada via USB, sem depender de drivers virtuais pesados.
✅ **Fake ACK Inteligente (Exclusivo):** 
Simula o comportamento bidirecional de impressoras Epson/Elgin. O seu PDV não vai mais travar (dar timeout ou erro de "String") esperando a impressora responder se está online!
✅ **Ponte Serial (Balanças e Equipamentos):** 
Recebe requisições de rede e as transmite para uma porta COM física ou virtual de forma contínua.
✅ **Invisível e Automático:** 
O programa roda como um Serviço do Windows protegido. Ele não fecha se o usuário deslogar, reinicia sozinho em caso de falha, e a interface gráfica abre escondida ali perto do relógio do Windows na inicialização.
✅ **Botão "Testar Impressão" Embutido:** 
Teste a comunicação com um único clique de dentro do próprio aplicativo, isolando problemas de rede do seu PDV.

---

## 2. Instalação Passo a Passo

O processo é quase 100% automático para facilitar a vida de quem está instalando no cliente:
1. Copie a pasta de instalação (ou o arquivo `OpenVSPE.Setup.exe`) para o computador que possui a impressora/balança ligada no cabo.
2. Dê um duplo-clique no **`OpenVSPE.Setup.exe`**.
3. Clique em **"Instalar"**.
   *O instalador irá baixar as dependências (.NET), configurar o Firewall, instalar o serviço e criar o atalho nas inicializações para abrir junto com o Windows.*
4. Ao final, clique em **"Concluir"**. O aplicativo abrirá na mesma hora.

> **Nota:** Nas próximas vezes que o PC for ligado, o ícone do OpenVSPE aparecerá escondido na bandeja do sistema (perto do relógio).

---

## 3. Como Configurar e Utilizar

Ao abrir o OpenVSPE, você verá a interface principal com uma lista das suas conexões (Canais).

### A) Criando um Canal para Impressoras USB (Printer RAW)
Se você quer que o seu PDV envie a impressão via Rede (IP) e ela saia numa impressora USB (Ex: Elgin, Bematech, Epson).

1. Clique em **"Novo"** na barra superior.
2. Preencha o **Nome** (Ex: Impressora Caixa 01).
3. Em **Tipo**, selecione a opção **Printer**.
4. Abaixo, no campo **NomeImpressora**, você **DEVE** digitar o nome da impressora **exatamente igual** a como ela aparece no "Painel de Controle -> Dispositivos e Impressoras" do Windows (Ex: `ELGIN I9` ou `Generic / Text Only`).
5. Em **Porta TCP**, defina a porta que o seu sistema de vendas usará para enviar a impressão (o padrão das impressoras de rede costuma ser **9100**).
6. Clique em **Salvar**, e na janela principal clique no botão **Salvar** novamente.

### B) Criando um Canal para Balanças (Serial)
Se você precisa conectar uma balança via porta COM mas seu sistema procura por ela na Rede.

1. Clique em **"Novo"**.
2. Preencha o **Nome** (Ex: Balança Filizola).
3. Em **Tipo**, selecione **Serial**.
4. Configure a **Porta COM** (Ex: `COM3`), e os parâmetros técnicos (BaudRate, DataBits, StopBits) conforme o manual da balança.
5. Em **Porta TCP**, coloque a porta que vai ouvir o seu sistema (Ex: `4001`).
6. Salve.

### C) Operações de Rotina
- **Iniciar/Parar:** Após criar um canal, clique no botão "Iniciar" (verde) na barra superior. O ícone ao lado do canal ficará verde e ele estará pronto para receber as requisições.
- **Botão Testar:** Selecione a sua impressora na lista e clique no ícone **"Testar"** na barra superior. Um cupom de teste sairá na impressora e, caso haja erro no nome digitado, o sistema o avisará claramente na tela.
- **Botão Logs:** Caso precise de ajuda avançada, clique em "Logs". Uma pasta abrirá listando tudo o que o OpenVSPE recebeu e processou naquele dia.

---

## Dúvidas Frequentes

**1. O meu sistema mostra "Timeout" ou "Erro de String vazia" ao tentar imprimir!**
Se o OpenVSPE já está aberto e o botão "Testar" funciona: vá no seu sistema de vendas e verifique as configurações (IP do computador onde o OpenVSPE está, e a Porta configuradas estão corretas). Cuidado para não deixar opções ou nomes em branco no cadastro da impressora do seu software.

**2. Eu clico em iniciar, mas o "Status" da impressora fica laranja "Erro".**
Isso acontece 99% das vezes quando o campo **NomeImpressora** está diferente do Windows. Vá no Painel de Controle de Impressoras, confira os espaços ou peça para o Windows renomear a impressora para um nome mais fácil (Ex: `CAIXA1`) e o coloque de forma idêntica no OpenVSPE.

**3. Preciso deixar a tela do OpenVSPE sempre aberta?**
**Não!** Você pode fechar no `X` da janela que ele vai se esconder do lado do relógio. A versão mais nova também inicia junto com o Windows de forma invisível. O OpenVSPE toma conta de tudo nos bastidores!
