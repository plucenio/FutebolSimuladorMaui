using Microsoft.Maui.Graphics;
using System.Timers;

namespace FutebolSimuladorMaui
{
    public partial class MainPage : ContentPage
    {
        private const float VelocidadePasse = 2.1f;
        private System.Timers.Timer gameTimer;
        private System.Timers.Timer relogioTimer;
        private int segundosRestantes = 45;

        private int placarCasa = 0;
        private int placarVisitante = 0;

        private Bola bola;
        private List<Jogador> timeCasa;
        private List<Jogador> timeVisitante;
        private List<Jogador>? timeComPosse;
        private Jogador? jogadorComPosse;
        private Jogador? jogadorComTempoDePosse;
        private float tempoComPosseSegundos;
        private Jogador? jogadorRecuando;
        private float tempoRecuando;
        private int quadrosAteNovoPasse;
        private DateTime ultimaAtualizacaoDoJogo;
        private RectF campoBounds;
        private FutebolDrawable drawable;

        public MainPage()
        {
            InitializeComponent();

            campoBounds = new RectF(20, 50, 760, 460);
            drawable = new FutebolDrawable
            {
                CampoX = campoBounds.X,
                CampoY = campoBounds.Y,
                CampoLargura = campoBounds.Width,
                CampoAltura = campoBounds.Height
            };

            // Atribuir o Drawable ao GraphicsView criado no XAML
            // (Certifique-se de nomear seu GraphicsView como x:Name="graphicsView" no XAML)
            myGraphicsView.Drawable = drawable;

            InicializarPartida();

            // Loop de Jogo (~60 FPS)
            gameTimer = new System.Timers.Timer(16);
            gameTimer.Elapsed += GameTimer_Elapsed;
            gameTimer.Start();

            // Cronômetro Regressivo de 45 segundos
            relogioTimer = new System.Timers.Timer(1000);
            relogioTimer.Elapsed += (s, e) => {
                if (segundosRestantes > 0)
                {
                    segundosRestantes--;
                    drawable.SegundosRestantes = segundosRestantes;
                }
                else
                {
                    relogioTimer.Stop();
                    gameTimer.Stop();
                    MainThread.BeginInvokeOnMainThread(async () => {
                        await DisplayAlert("Fim de Jogo!", $"Placar Final:\nVermelhos {placarCasa} x {placarVisitante} Azuis", "OK");
                    });
                }
            };
            relogioTimer.Start();
        }

        private void InicializarPartida()
        {
            bola = new Bola(campoBounds.X + campoBounds.Width / 2, campoBounds.Y + campoBounds.Height / 2);
            drawable.Bola = bola;

            timeCasa = CriarTime(Colors.Red, defendeLadoEsquerdo: true);
            drawable.TimeCasa = timeCasa;

            timeVisitante = CriarTime(Colors.Blue, defendeLadoEsquerdo: false);
            drawable.TimeVisitante = timeVisitante;
        }

        private List<Jogador> CriarTime(Color cor, bool defendeLadoEsquerdo)
        {
            float faixaEsquerda = defendeLadoEsquerdo ? 0.14f : 0.86f;
            float faixaDireita = 1 - faixaEsquerda;
            float zagueiroEsquerdo = defendeLadoEsquerdo ? 0.38f : 0.62f;
            float zagueiroDireito = 1 - zagueiroEsquerdo;

            (float X, float Y, PosicaoJogador Posicao)[] formacao =
            {
                (0.03f, 0.50f, PosicaoJogador.Goleiro),
                (0.18f, zagueiroEsquerdo, PosicaoJogador.ZagueiroEsquerdo),
                (0.18f, zagueiroDireito, PosicaoJogador.ZagueiroDireito),
                (0.16f, faixaEsquerda, PosicaoJogador.LateralEsquerdo),
                (0.16f, faixaDireita, PosicaoJogador.LateralDireito),
                (0.43f, 0.14f, PosicaoJogador.MeioCampo),
                (0.43f, 0.38f, PosicaoJogador.MeioCampo),
                (0.43f, 0.62f, PosicaoJogador.MeioCampo),
                (0.43f, 0.86f, PosicaoJogador.MeioCampo),
                (0.70f, 0.38f, PosicaoJogador.Atacante),
                (0.70f, 0.62f, PosicaoJogador.Atacante)
            };

            return formacao.Select(posicao =>
            {
                float proporcaoX = defendeLadoEsquerdo ? posicao.X : 1 - posicao.X;
                float x = campoBounds.X + campoBounds.Width * proporcaoX;
                float y = campoBounds.Y + campoBounds.Height * posicao.Y;
                return new Jogador(x, y, cor, posicao.Posicao, Random.Shared.Next(0, 101), defendeLadoEsquerdo);
            }).ToList();
        }

        private void GameTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            float deltaSegundos = ultimaAtualizacaoDoJogo == default
                ? (float)gameTimer.Interval / 1000f
                : (float)Math.Clamp((e.SignalTime - ultimaAtualizacaoDoJogo).TotalSeconds, 0, 0.1);
            ultimaAtualizacaoDoJogo = e.SignalTime;

            if (quadrosAteNovoPasse > 0)
                quadrosAteNovoPasse--;

            bola.Atualizar(campoBounds);
            float inicioGolY = campoBounds.Y + (campoBounds.Height - 140) / 2;
            float fimGolY = inicioGolY + 140;

            // Detecção de Gols
            if (bola.X <= campoBounds.X + 10 && bola.Y >= inicioGolY && bola.Y <= fimGolY)
            {
                placarVisitante++;
                drawable.PlacarVisitante = placarVisitante;
                ReiniciarPosicoes();
            }
            else if (bola.X >= campoBounds.X + campoBounds.Width - 10 && bola.Y >= inicioGolY && bola.Y <= fimGolY)
            {
                placarCasa++;
                drawable.PlacarCasa = placarCasa;
                ReiniciarPosicoes();
            }

            AtualizarPosse();
            AtualizarTemporizadorPosse(0, reiniciar: false);
            Jogador? jogadorPressionando = null;
            bool acaoComBolaRealizada = false;
            bool protegendoBola = false;
            Jogador? portadorNesteQuadro = null;
            float posicaoXInicialDoPortador = 0;

            if (timeComPosse is not null && jogadorComPosse is not null)
            {
                var timeDefendendo = ReferenceEquals(timeComPosse, timeCasa) ? timeVisitante : timeCasa;
                jogadorPressionando = EncontrarMaisProximo(timeDefendendo);
                bool mesmoPortador = ReferenceEquals(jogadorComPosse, jogadorComTempoDePosse);
                bool passeForcado = (ReferenceEquals(jogadorComPosse, jogadorRecuando) && tempoRecuando >= 2)
                    || (mesmoPortador && tempoComPosseSegundos >= 2);
                bool passePorTempo = mesmoPortador && tempoComPosseSegundos >= 1;
                acaoComBolaRealizada = TentarPasse(jogadorPressionando, passeForcado, passePorTempo);
                if (!acaoComBolaRealizada && DistanciaEntre(jogadorComPosse, jogadorPressionando) < 85)
                    protegendoBola = true;

                portadorNesteQuadro = jogadorComPosse;
                posicaoXInicialDoPortador = portadorNesteQuadro.X;
                MoverTime(timeComPosse, jogadorComPosse, temPosse: true,
                    protegendoBola ? jogadorPressionando : null);
                MoverTime(timeDefendendo, jogadorPressionando, temPosse: false);
            }
            else
            {
                var jogadorCasaProximo = EncontrarMaisProximo(timeCasa);
                var jogadorVisitanteProximo = EncontrarMaisProximo(timeVisitante);
                MoverTime(timeCasa, jogadorCasaProximo, temPosse: false);
                MoverTime(timeVisitante, jogadorVisitanteProximo, temPosse: false);
            }

            EvitarSobreposicaoEntreJogadores();
            if (!acaoComBolaRealizada)
            {
                var timeAntesDoContato = timeComPosse;
                var portadorAntesDoContato = jogadorComPosse;
                AtualizarPosse();

                if (!ReferenceEquals(timeAntesDoContato, timeComPosse) ||
                    !ReferenceEquals(portadorAntesDoContato, jogadorComPosse))
                {
                    protegendoBola = false;
                    jogadorPressionando = timeComPosse is null
                        ? null
                        : EncontrarMaisProximo(ReferenceEquals(timeComPosse, timeCasa) ? timeVisitante : timeCasa);
                }
            }

            AtualizarTempoRecuo(portadorNesteQuadro, posicaoXInicialDoPortador, deltaSegundos, acaoComBolaRealizada);

            if (protegendoBola && jogadorComPosse is not null && jogadorPressionando is not null)
                ProtegerBola(jogadorComPosse, jogadorPressionando);

            bool bolaChutada = false;
            if (!acaoComBolaRealizada && !protegendoBola)
            {
                Jogador jogadorBolaCasa = timeComPosse is null
                    ? EncontrarMaisProximo(timeCasa)
                    : ReferenceEquals(timeComPosse, timeCasa) ? jogadorComPosse! : jogadorPressionando!;
                Jogador jogadorBolaVisitante = timeComPosse is null
                    ? EncontrarMaisProximo(timeVisitante)
                    : ReferenceEquals(timeComPosse, timeVisitante) ? jogadorComPosse! : jogadorPressionando!;
                bolaChutada = VerificarChute(jogadorBolaCasa);
                if (!bolaChutada)
                    bolaChutada = VerificarChute(jogadorBolaVisitante);
            }

            AtualizarTemporizadorPosse(deltaSegundos, acaoComBolaRealizada || bolaChutada);

            // Atualizar tela na thread principal
            MainThread.BeginInvokeOnMainThread(() =>
            {
                myGraphicsView.Invalidate();
            });
        }

        private void AtualizarPosse()
        {
            var jogadorCasaProximo = EncontrarMaisProximo(timeCasa);
            var jogadorVisitanteProximo = EncontrarMaisProximo(timeVisitante);
            float distanciaCasa = DistanciaAteBola(jogadorCasaProximo);
            float distanciaVisitante = DistanciaAteBola(jogadorVisitanteProximo);
            Jogador candidato = distanciaCasa <= distanciaVisitante ? jogadorCasaProximo : jogadorVisitanteProximo;
            float distanciaCandidato = Math.Min(distanciaCasa, distanciaVisitante);

            if (distanciaCandidato <= candidato.Raio + bola.Raio)
            {
                timeComPosse = timeCasa.Contains(candidato) ? timeCasa : timeVisitante;
                jogadorComPosse = candidato;
            }
            else if (timeComPosse is null)
            {
                jogadorComPosse = null;
            }
            else if (jogadorComPosse is null)
            {
                jogadorComPosse = EncontrarMaisProximo(timeComPosse);
            }
        }

        private void MoverTime(
            List<Jogador> time,
            Jogador jogadorAlvoDaBola,
            bool temPosse,
            Jogador? adversarioParaProtecao = null)
        {
            foreach (var jogador in time)
            {
                (float X, float Y) alvo;
                if (ReferenceEquals(jogador, jogadorAlvoDaBola))
                    alvo = adversarioParaProtecao is null
                        ? (bola.X, bola.Y)
                        : CalcularPosicaoDeProtecao(jogador, adversarioParaProtecao);
                else
                    alvo = temPosse ? CalcularPosicaoDeApoio(jogador) : CalcularPosicaoDefensiva(jogador);

                jogador.MoverEmDirecao(alvo.X, alvo.Y, campoBounds, bola.X);
            }
        }

        private bool TentarPasse(
            Jogador jogadorPressionando,
            bool passeForcado = false,
            bool passePorTempo = false)
        {
            if (timeComPosse is null || jogadorComPosse is null)
                return false;

            bool bolaPertoDoCanto = EstaPertoDoCanto();
            if (!passeForcado && !passePorTempo &&
                (quadrosAteNovoPasse > 0 ||
                 (DistanciaEntre(jogadorComPosse, jogadorPressionando) >= 85 && !bolaPertoDoCanto)))
                return false;

            var adversarios = ReferenceEquals(timeComPosse, timeCasa) ? timeVisitante : timeCasa;
            Jogador? recebedor = null;
            float maiorPontuacao = passeForcado || passePorTempo ? float.NegativeInfinity : 45;
            (float X, float Y) alvoPasse = default;

            foreach (var candidato in timeComPosse)
            {
                if (ReferenceEquals(candidato, jogadorComPosse) || candidato.Posicao == PosicaoJogador.Goleiro)
                    continue;

                var alvo = CalcularPosicaoDeApoio(candidato);
                float distanciaPasse = DistanciaEntrePontos(jogadorComPosse.X, jogadorComPosse.Y, alvo.X, alvo.Y);
                if (distanciaPasse is < 30 or > 400)
                    continue;

                float distanciaAdversario = adversarios
                    .Min(adversario => DistanciaEntrePontos(alvo.X, alvo.Y, adversario.X, adversario.Y));
                if (distanciaAdversario < 40)
                    continue;

                float progresso = (alvo.X - bola.X) * (jogadorComPosse.DefendeLadoEsquerdo ? 1 : -1);
                float pontuacao = candidato.Habilidade * 0.45f
                    + Math.Min(distanciaAdversario, 130) * 0.45f
                    + Math.Clamp(progresso, -100, 180) * 0.25f
                    - distanciaPasse * 0.10f;

                if (progresso < 0)
                    pontuacao -= 10;

                if (pontuacao > maiorPontuacao)
                {
                    maiorPontuacao = pontuacao;
                    recebedor = candidato;
                    alvoPasse = alvo;
                }
            }

            if (recebedor is null)
            {
                if (!passeForcado && !bolaPertoDoCanto)
                    return false;

                ChutarParaFrente(jogadorComPosse);
                return true;
            }

            float dx = alvoPasse.X - bola.X;
            float dy = alvoPasse.Y - bola.Y;
            float distancia = (float)Math.Sqrt(dx * dx + dy * dy);
            if (distancia >= 1)
            {
                bola.VelX = dx / distancia * VelocidadePasse;
                bola.VelY = dy / distancia * VelocidadePasse;
            }

            jogadorComPosse = recebedor;
            quadrosAteNovoPasse = 40;
            jogadorRecuando = null;
            tempoRecuando = 0;
            return true;
        }

        private bool EstaPertoDoCanto()
        {
            float margemX = campoBounds.Width * 0.12f;
            float margemY = campoBounds.Height * 0.15f;
            bool pertoDaExtremidade = bola.X <= campoBounds.X + margemX ||
                bola.X >= campoBounds.X + campoBounds.Width - margemX;
            bool pertoDaLateral = bola.Y <= campoBounds.Y + margemY ||
                bola.Y >= campoBounds.Y + campoBounds.Height - margemY;
            return pertoDaExtremidade && pertoDaLateral;
        }

        private void ChutarParaFrente(Jogador portador)
        {
            float alvoX = portador.DefendeLadoEsquerdo
                ? campoBounds.X + campoBounds.Width
                : campoBounds.X;
            float alvoY = campoBounds.Y + campoBounds.Height / 2;
            float dx = alvoX - bola.X;
            float dy = alvoY - bola.Y;
            float distancia = (float)Math.Sqrt(dx * dx + dy * dy);

            bola.VelX = dx / distancia * 7f;
            bola.VelY = dy / distancia * 7f;
            quadrosAteNovoPasse = 40;
            jogadorRecuando = null;
            tempoRecuando = 0;
        }

        private (float X, float Y) CalcularPosicaoDeProtecao(Jogador portador, Jogador adversario)
        {
            float dx = bola.X - adversario.X;
            float dy = bola.Y - adversario.Y;
            float distancia = (float)Math.Sqrt(dx * dx + dy * dy);
            if (distancia < 0.001f)
            {
                dx = portador.DefendeLadoEsquerdo ? 1 : -1;
                dy = 0;
                distancia = 1;
            }

            float afastamento = portador.Raio + bola.Raio + 1;
            float alvoX = bola.X - dx / distancia * afastamento;
            float alvoY = bola.Y - dy / distancia * afastamento;
            return (
                Math.Clamp(alvoX, campoBounds.X + portador.Raio, campoBounds.X + campoBounds.Width - portador.Raio),
                Math.Clamp(alvoY, campoBounds.Y + portador.Raio, campoBounds.Y + campoBounds.Height - portador.Raio));
        }

        private void ProtegerBola(Jogador portador, Jogador adversario)
        {
            float dx = portador.X - adversario.X;
            float dy = portador.Y - adversario.Y;
            float distancia = (float)Math.Sqrt(dx * dx + dy * dy);
            if (distancia < 0.001f)
            {
                dx = portador.DefendeLadoEsquerdo ? 1 : -1;
                dy = 0;
                distancia = 1;
            }

            float afastamento = portador.Raio + bola.Raio + 1;
            bola.X = Math.Clamp(portador.X + dx / distancia * afastamento, campoBounds.X + bola.Raio,
                campoBounds.X + campoBounds.Width - bola.Raio);
            bola.Y = Math.Clamp(portador.Y + dy / distancia * afastamento, campoBounds.Y + bola.Raio,
                campoBounds.Y + campoBounds.Height - bola.Raio);
            bola.VelX = 0;
            bola.VelY = 0;
        }

        private (float X, float Y) CalcularPosicaoDeApoio(Jogador jogador)
        {
            float direcaoAtaque = jogador.DefendeLadoEsquerdo ? 1 : -1;
            float centroY = campoBounds.Y + campoBounds.Height / 2;
            float deslocamentoFaixa = (jogador.PosicaoInicialY - centroY) * 0.75f
                + (bola.Y - centroY) * 0.25f;
            float alvoX = jogador.Posicao switch
            {
                PosicaoJogador.Goleiro => jogador.PosicaoInicialX,
                PosicaoJogador.ZagueiroEsquerdo or PosicaoJogador.ZagueiroDireito =>
                    jogador.PosicaoInicialX + direcaoAtaque * 12,
                PosicaoJogador.LateralEsquerdo or PosicaoJogador.LateralDireito =>
                    jogador.PosicaoInicialX + direcaoAtaque * 24,
                PosicaoJogador.MeioCampo =>
                    bola.X + direcaoAtaque * (85 + jogador.Habilidade * 0.25f),
                PosicaoJogador.Atacante =>
                    bola.X + direcaoAtaque * (100 + jogador.Habilidade * 0.35f),
                _ => jogador.PosicaoInicialX
            };
            float alvoY = centroY + deslocamentoFaixa;

            return (
                Math.Clamp(alvoX, campoBounds.X + jogador.Raio, campoBounds.X + campoBounds.Width - jogador.Raio),
                Math.Clamp(alvoY, campoBounds.Y + jogador.Raio, campoBounds.Y + campoBounds.Height - jogador.Raio));
        }

        private (float X, float Y) CalcularPosicaoDefensiva(Jogador jogador)
        {
            if (jogador.Posicao == PosicaoJogador.Goleiro)
                return (jogador.PosicaoInicialX, jogador.PosicaoInicialY);

            float centroX = campoBounds.X + campoBounds.Width / 2;
            float centroY = campoBounds.Y + campoBounds.Height / 2;
            float deslocamentoX = Math.Clamp((bola.X - centroX) * 0.12f, -45, 45);
            float deslocamentoY = (bola.Y - centroY) * 0.20f;

            return (
                jogador.PosicaoInicialX + deslocamentoX,
                centroY + (jogador.PosicaoInicialY - centroY) * 0.80f + deslocamentoY);
        }

        private Jogador EncontrarMaisProximo(List<Jogador> time)
        {
            Jogador maisProximo = time[0];
            float menorDistancia = DistanciaAteBola(maisProximo);

            foreach (var jogador in time.Skip(1))
            {
                float distancia = DistanciaAteBola(jogador);
                if (distancia < menorDistancia)
                {
                    maisProximo = jogador;
                    menorDistancia = distancia;
                }
            }

            return maisProximo;
        }

        private float DistanciaAteBola(Jogador jogador)
        {
            return DistanciaEntrePontos(jogador.X, jogador.Y, bola.X, bola.Y);
        }

        private void AtualizarTempoRecuo(Jogador? portador, float posicaoXInicial, float deltaSegundos, bool passeRealizado)
        {
            if (passeRealizado || portador is null || !ReferenceEquals(portador, jogadorComPosse))
            {
                jogadorRecuando = null;
                tempoRecuando = 0;
                return;
            }

            if (!ReferenceEquals(jogadorRecuando, portador))
            {
                jogadorRecuando = portador;
                tempoRecuando = 0;
            }

            float deslocamento = portador.X - posicaoXInicial;
            float direcaoAtaque = portador.DefendeLadoEsquerdo ? 1 : -1;
            float deslocamentoNaDirecaoDoAtaque = deslocamento * direcaoAtaque;

            if (deslocamentoNaDirecaoDoAtaque < -0.05f)
                tempoRecuando += deltaSegundos;
            else if (deslocamentoNaDirecaoDoAtaque > 0.05f)
                tempoRecuando = 0;
        }

        private void AtualizarTemporizadorPosse(float deltaSegundos, bool reiniciar)
        {
            if (timeComPosse is null || jogadorComPosse is null)
            {
                jogadorComTempoDePosse = null;
                tempoComPosseSegundos = 0;
                return;
            }

            if (!ReferenceEquals(jogadorComTempoDePosse, jogadorComPosse))
            {
                jogadorComTempoDePosse = jogadorComPosse;
                tempoComPosseSegundos = 0;
            }
            else if (reiniciar)
            {
                tempoComPosseSegundos = 0;
            }
            else
            {
                tempoComPosseSegundos += deltaSegundos;
            }
        }

        private static float DistanciaEntre(Jogador primeiro, Jogador segundo)
        {
            return DistanciaEntrePontos(primeiro.X, primeiro.Y, segundo.X, segundo.Y);
        }

        private static float DistanciaEntrePontos(float x1, float y1, float x2, float y2)
        {
            float dx = x1 - x2;
            float dy = y1 - y2;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        private void EvitarSobreposicaoEntreJogadores()
        {
            var jogadores = timeCasa.Concat(timeVisitante).ToArray();

            for (int tentativa = 0; tentativa < 16; tentativa++)
            {
                bool houveSobreposicao = false;

                for (int i = 0; i < jogadores.Length; i++)
                {
                    for (int j = i + 1; j < jogadores.Length; j++)
                    {
                        var primeiro = jogadores[i];
                        var segundo = jogadores[j];
                        float dx = segundo.X - primeiro.X;
                        float dy = segundo.Y - primeiro.Y;
                        float distancia = (float)Math.Sqrt(dx * dx + dy * dy);
                        float distanciaMinima = primeiro.Raio + segundo.Raio + 1;

                        if (distancia >= distanciaMinima)
                            continue;

                        houveSobreposicao = true;
                        if (distancia < 0.001f)
                        {
                            dx = 1;
                            dy = ((i + j) % 2 == 0) ? 1 : -1;
                            distancia = (float)Math.Sqrt(dx * dx + dy * dy);
                        }

                        float deslocamento = (distanciaMinima - distancia) / 2;
                        float deslocamentoX = dx / distancia * deslocamento;
                        float deslocamentoY = dy / distancia * deslocamento;
                        primeiro.X -= deslocamentoX;
                        primeiro.Y -= deslocamentoY;
                        segundo.X += deslocamentoX;
                        segundo.Y += deslocamentoY;
                        primeiro.AplicarLimites(campoBounds, bola.X);
                        segundo.AplicarLimites(campoBounds, bola.X);
                    }
                }

                if (!houveSobreposicao)
                    break;
            }
        }

        private bool VerificarChute(Jogador jogador)
        {
            if (DistanciaAteBola(jogador) <= jogador.Raio + bola.Raio)
            {
                float angulo = (float)(Random.Shared.NextDouble() * Math.PI * 2);
                bola.VelX = (float)Math.Cos(angulo) * 6f;
                bola.VelY = (float)Math.Sin(angulo) * 6f;
                return true;
            }

            return false;
        }

        private void ReiniciarPosicoes()
        {
            bola.X = campoBounds.X + campoBounds.Width / 2;
            bola.Y = campoBounds.Y + campoBounds.Height / 2;
            bola.VelX = 0;
            bola.VelY = 0;
            timeComPosse = null;
            jogadorComPosse = null;
            jogadorComTempoDePosse = null;
            tempoComPosseSegundos = 0;
            jogadorRecuando = null;
            tempoRecuando = 0;
            quadrosAteNovoPasse = 0;
            InicializarPosicoesJogadores();
        }

        private void InicializarPosicoesJogadores()
        {
            foreach (var jogador in timeCasa)
                jogador.ReiniciarPosicao();
            foreach (var jogador in timeVisitante)
                jogador.ReiniciarPosicao();
        }
    }
}