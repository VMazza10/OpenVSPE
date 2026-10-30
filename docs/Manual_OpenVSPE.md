# Manual do usuário do OpenVSPE

O OpenVSPE liga o sistema de vendas (PDV) às impressoras e balanças da loja. Muitos PDVs, principalmente os antigos ou muito específicos, só sabem mandar impressão pela rede (IP) ou ler o peso da balança por uma porta serial (COM). O OpenVSPE faz a ponte entre esses dois lados e roda em segundo plano, sem ninguém precisar abrir nada.

## O que ele faz

Na impressão, ele recebe os comandos que o PDV manda para um IP e uma porta e entrega direto na impressora USB, em modo RAW, sem depender de driver virtual.

Alguns PDVs perguntam à impressora se ela está online antes de imprimir e travam com timeout ou erro de "String" quando não recebem resposta. O OpenVSPE responde a essas consultas no lugar dela, imitando o comportamento das impressoras Epson e Elgin. Chamamos isso de Fake ACK.

Nas balanças e em outros equipamentos seriais, ele recebe as requisições pela rede e repassa para uma porta COM, física ou virtual.

O programa roda como serviço do Windows. Ele continua funcionando quando o usuário sai da sessão e reinicia sozinho se falhar. A interface abre minimizada perto do relógio quando o Windows inicia.

Dentro do próprio aplicativo há um botão "Testar" que imprime um cupom de teste. Assim dá para saber se o problema está no OpenVSPE ou na configuração do PDV.

## Instalação

A instalação é quase toda automática:

1. Baixe o `.zip` da Release, extraia e copie a pasta inteira para o computador onde a impressora ou a balança está ligada no cabo. O instalador (`OpenVSPE_V*.exe`) precisa das pastas `ui` e `service` ao lado dele.
2. Dê dois cliques no instalador e confirme a permissão de administrador.
3. Clique em "Instalar". O instalador baixa o .NET se precisar, configura o firewall, instala o serviço e cria o atalho que abre o programa junto com o Windows.
4. No final, clique em "Concluir". O aplicativo abre na hora.

Depois disso, sempre que o computador ligar, o ícone do OpenVSPE fica na bandeja do sistema, perto do relógio.

## Como configurar

A tela principal mostra a lista de conexões, que o programa chama de canais.

### Impressora USB (canal Printer)

Use este tipo quando o PDV manda a impressão pela rede e ela precisa sair numa impressora USB, como Elgin, Bematech ou Epson.

1. Clique em "Novo" na barra superior.
2. Preencha o Nome (por exemplo, Impressora Caixa 01).
3. Em Tipo, escolha Printer.
4. No campo NomeImpressora, digite o nome da impressora exatamente como aparece no Windows, em Painel de Controle > Dispositivos e Impressoras (por exemplo, `ELGIN I9` ou `Generic / Text Only`). Se o nome tiver qualquer diferença, a impressão não sai.
5. Em Porta TCP, coloque a porta que o sistema de vendas usa para imprimir. Impressoras de rede costumam usar a 9100.
6. Clique em "Salvar" na janela do canal e depois em "Salvar" de novo na janela principal.

### Balança (canal Serial)

Use este tipo quando a balança está numa porta COM, mas o sistema de vendas procura por ela na rede.

1. Clique em "Novo".
2. Preencha o Nome (por exemplo, Balança Filizola).
3. Em Tipo, escolha Serial.
4. Configure a Porta COM (por exemplo, `COM3`) e os parâmetros BaudRate, DataBits e StopBits conforme o manual da balança.
5. Em Porta TCP, coloque a porta em que o sistema vai se conectar (por exemplo, `4001`).
6. Salve.

### No dia a dia

Depois de criar um canal, clique em "Iniciar" (o botão verde da barra superior). Quando o ícone ao lado do canal ficar verde, ele está pronto para receber as requisições. "Parar" desliga o canal.

Para testar uma impressora, selecione-a na lista e clique em "Testar". Um cupom de teste sai na impressora. Se o nome estiver digitado errado, o programa avisa na tela.

O botão "Logs" abre a pasta com tudo o que o OpenVSPE recebeu e processou no dia. É o primeiro lugar para olhar quando algo não funciona.

## Dúvidas frequentes

### O sistema de vendas mostra "Timeout" ou "Erro de String vazia" ao imprimir

Se o OpenVSPE está aberto e o botão "Testar" funciona, o problema está na configuração do sistema de vendas. Confira se o IP do computador onde o OpenVSPE está e a porta estão corretos, e não deixe opções ou nomes em branco no cadastro da impressora.

### Cliquei em "Iniciar", mas o status da impressora fica laranja, em "Erro"

Quase sempre o campo NomeImpressora está diferente do nome no Windows. Abra o painel de impressoras e confira os espaços. Se ficar difícil, renomeie a impressora no Windows para algo simples, como `CAIXA1`, e use exatamente esse nome no OpenVSPE.

### Preciso deixar a tela do OpenVSPE aberta?

Não. Ao fechar no X, a janela vai para a bandeja, perto do relógio, e o serviço continua rodando. O programa também inicia sozinho com o Windows.
