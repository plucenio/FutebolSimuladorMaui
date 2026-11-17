using Microsoft.Maui.Graphics;
using System.Timers;

namespace FutebolSimuladorMaui
{
    public partial class MainPage : ContentPage
    {
        private System.Timers.Timer gameTimer;
        private System.Timers.Timer relogioTimer;
        private int segundosRestantes = 45;

        private int placarCasa = 0;
        private int placarVisitante = 0;

        private Bola bola;
        private List<Jogador> timeCasa;
        private List<Jogador> timeVisitante;
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
                (0.04f, 0.50f, PosicaoJogador.Goleiro),
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

            // Movimentação dos Jogadores
            foreach (var j in timeCasa) j.MoverEmDirecao(bola.X, bola.Y, campoBounds);
            foreach (var j in timeVisitante) j.MoverEmDirecao(bola.X, bola.Y, campoBounds);
            EvitarSobreposicaoEntreJogadores();

            VerificarChutes(timeCasa);
            VerificarChutes(timeVisitante);

            // Atualizar tela na thread principal
            MainThread.BeginInvokeOnMainThread(() =>
            {
                myGraphicsView.Invalidate();
            });
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
                        primeiro.AplicarLimites(campoBounds);
                        segundo.AplicarLimites(campoBounds);
                    }
                }

                if (!houveSobreposicao)
                    break;
            }
        }

        private void VerificarChutes(List<Jogador> time)
        {
            foreach (var j in time)
            {
                float distancia = (float)Math.Sqrt(Math.Pow(j.X - bola.X, 2) + Math.Pow(j.Y - bola.Y, 2));
                if (distancia < 20)
                {
                    Random rnd = new Random();
                    float angulo = (float)(rnd.NextDouble() * Math.PI * 2);
                    bola.VelX = (float)Math.Cos(angulo) * 6f;
                    bola.VelY = (float)Math.Sin(angulo) * 6f;
                }
            }
        }

        private void ReiniciarPosicoes()
        {
            bola.X = campoBounds.X + campoBounds.Width / 2;
            bola.Y = campoBounds.Y + campoBounds.Height / 2;
            bola.VelX = 0;
            bola.VelY = 0;
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