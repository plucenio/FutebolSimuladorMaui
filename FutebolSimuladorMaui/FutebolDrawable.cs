using Microsoft.Maui.Graphics;

namespace FutebolSimuladorMaui
{
    public static class CampoFutebol
    {
        public static RectF CalcularGrandeArea(RectF campo, bool ladoEsquerdo)
        {
            float largura = campo.Width * 0.16f;
            float altura = campo.Height * 0.60f;
            float x = ladoEsquerdo ? campo.X : campo.X + campo.Width - largura;
            float y = campo.Y + (campo.Height - altura) / 2;
            return new RectF(x, y, largura, altura);
        }

        public static RectF CalcularPequenaArea(RectF campo, bool ladoEsquerdo)
        {
            float largura = campo.Width * 0.055f;
            float altura = campo.Height * 0.40f;
            float x = ladoEsquerdo ? campo.X : campo.X + campo.Width - largura;
            float y = campo.Y + (campo.Height - altura) / 2;
            return new RectF(x, y, largura, altura);
        }
    }

    public enum PosicaoJogador
    {
        Goleiro,
        ZagueiroEsquerdo,
        ZagueiroDireito,
        LateralEsquerdo,
        LateralDireito,
        MeioCampo,
        Atacante
    }

    public class FutebolDrawable : IDrawable
    {
        public float CampoX { get; set; } = 20;
        public float CampoY { get; set; } = 40;
        public float CampoLargura { get; set; } = 720;
        public float CampoAltura { get; set; } = 420;

        public Bola Bola { get; set; }
        public List<Jogador> TimeCasa { get; set; }
        public List<Jogador> TimeVisitante { get; set; }

        public int PlacarCasa { get; set; }
        public int PlacarVisitante { get; set; }
        public int SegundosRestantes { get; set; }

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            // 1. Fundo do Campo (Gramado)
            canvas.FillColor = Colors.ForestGreen;
            canvas.FillRectangle(CampoX, CampoY, CampoLargura, CampoAltura);

            // Linhas do Campo
            canvas.StrokeColor = Colors.White;
            canvas.StrokeSize = 2;
            canvas.DrawRectangle(CampoX, CampoY, CampoLargura, CampoAltura);

            // Linha do Meio-Campo
            canvas.DrawLine(CampoX + CampoLargura / 2, CampoY, CampoX + CampoLargura / 2, CampoY + CampoAltura);
            
            // Círculo Central
            canvas.DrawEllipse(CampoX + CampoLargura / 2 - 50, CampoY + CampoAltura / 2 - 50, 100, 100);

            // Gols
            float golY = CampoY + (CampoAltura - 140) / 2;
            DesenharGol(canvas, CampoX - 18, golY);
            DesenharGol(canvas, CampoX + CampoLargura, golY);

            var campo = new RectF(CampoX, CampoY, CampoLargura, CampoAltura);
            var grandeAreaCasa = CampoFutebol.CalcularGrandeArea(campo, ladoEsquerdo: true);
            var grandeAreaVisitante = CampoFutebol.CalcularGrandeArea(campo, ladoEsquerdo: false);
            var pequenaAreaCasa = CampoFutebol.CalcularPequenaArea(campo, ladoEsquerdo: true);
            var pequenaAreaVisitante = CampoFutebol.CalcularPequenaArea(campo, ladoEsquerdo: false);
            canvas.DrawRectangle(grandeAreaCasa.X, grandeAreaCasa.Y, grandeAreaCasa.Width, grandeAreaCasa.Height);
            canvas.DrawRectangle(grandeAreaVisitante.X, grandeAreaVisitante.Y, grandeAreaVisitante.Width, grandeAreaVisitante.Height);
            canvas.DrawRectangle(pequenaAreaCasa.X, pequenaAreaCasa.Y, pequenaAreaCasa.Width, pequenaAreaCasa.Height);
            canvas.DrawRectangle(pequenaAreaVisitante.X, pequenaAreaVisitante.Y, pequenaAreaVisitante.Width, pequenaAreaVisitante.Height);

            // 2. Placar e Tempo
            canvas.FontColor = Colors.Black;
            canvas.FontSize = 16;
            string textoPlacar = $"Vermelho {PlacarCasa} x {PlacarVisitante} Azul  |  Tempo: {SegundosRestantes}s";
            canvas.DrawString(textoPlacar, CampoX, 10, CampoLargura, 30, HorizontalAlignment.Left, VerticalAlignment.Center);

            // 3. Desenhar Bola
            if (Bola != null)
            {
                canvas.FillColor = Colors.White;
                canvas.StrokeColor = Colors.Black;
                canvas.FillEllipse(Bola.X - Bola.Raio, Bola.Y - Bola.Raio, Bola.Raio * 2, Bola.Raio * 2);
                canvas.DrawEllipse(Bola.X - Bola.Raio, Bola.Y - Bola.Raio, Bola.Raio * 2, Bola.Raio * 2);
            }

            // 4. Desenhar Jogadores
            if (TimeCasa != null)
            {
                foreach (var j in TimeCasa) j.Desenhar(canvas);
            }
            if (TimeVisitante != null)
            {
                foreach (var j in TimeVisitante) j.Desenhar(canvas);
            }
        }

        private static void DesenharGol(ICanvas canvas, float x, float y)
        {
            const float profundidade = 18;
            const float altura = 140;

            canvas.FillColor = Colors.White;
            canvas.FillRectangle(x, y, profundidade, altura);
            canvas.StrokeColor = Colors.DarkSlateGray;
            canvas.StrokeSize = 3;
            canvas.DrawRectangle(x, y, profundidade, altura);

            canvas.StrokeColor = Colors.LightGray;
            canvas.StrokeSize = 1;
            for (int linha = 1; linha < 4; linha++)
            {
                float linhaY = y + altura * linha / 4;
                canvas.DrawLine(x, linhaY, x + profundidade, linhaY);
            }
            canvas.DrawLine(x + profundidade / 2, y, x + profundidade / 2, y + altura);
        }
    }

    public class Bola
    {
        public float X, Y;
        public float VelX = 0, VelY = 0;
        public float Raio = 4;

        public Bola(float x, float y) { X = x; Y = y; }

        public void Atualizar(RectF campo)
        {
            X += VelX;
            Y += VelY;

            if (X - Raio <= campo.X) { X = campo.X + Raio; VelX = -VelX; }
            if (X + Raio >= campo.X + campo.Width) { X = campo.X + campo.Width - Raio; VelX = -VelX; }
            if (Y - Raio <= campo.Y) { Y = campo.Y + Raio; VelY = -VelY; }
            if (Y + Raio >= campo.Y + campo.Height) { Y = campo.Y + campo.Height - Raio; VelY = -VelY; }
        }
    }

    public class Jogador
    {
        public float X, Y;
        public Color Cor;
        public float Raio = 8.4f;
        public PosicaoJogador Posicao { get; }
        public int Habilidade { get; }
        public bool DefendeLadoEsquerdo { get; }
        public float PosicaoInicialX => posicaoInicialX;
        public float PosicaoInicialY => posicaoInicialY;
        private readonly float posicaoInicialX;
        private readonly float posicaoInicialY;
        private bool estaVoltandoParaPequenaArea;
        private float tempoInativoSegundos;
        public float VelocidadeMovimento => (0.9f + Habilidade / 100f * 1.8f) * 0.7f;
        public bool EstaInativo => tempoInativoSegundos > 0;

        public Jogador(float x, float y, Color cor, PosicaoJogador posicao, int habilidade, bool defendeLadoEsquerdo)
        {
            if (habilidade is < 0 or > 100)
                throw new ArgumentOutOfRangeException(nameof(habilidade), "A habilidade deve estar entre 0 e 100.");

            X = posicaoInicialX = x;
            Y = posicaoInicialY = y;
            Cor = cor;
            Posicao = posicao;
            Habilidade = habilidade;
            DefendeLadoEsquerdo = defendeLadoEsquerdo;
        }

        public void MoverEmDirecao(float alvoX, float alvoY, RectF campo, float bolaX)
        {
            if (EstaInativo)
                return;

            if (Posicao == PosicaoJogador.Goleiro &&
                !BolaNoCampoAdversario(bolaX, campo) &&
                (estaVoltandoParaPequenaArea || !EstaDentroDaPequenaArea(campo)))
            {
                estaVoltandoParaPequenaArea = true;
                var pequenaArea = CampoFutebol.CalcularPequenaArea(campo, DefendeLadoEsquerdo);
                float alvoRetornoX = Math.Clamp(X, pequenaArea.X + Raio, pequenaArea.X + pequenaArea.Width - Raio);
                float alvoRetornoY = Math.Clamp(Y, pequenaArea.Y + Raio, pequenaArea.Y + pequenaArea.Height - Raio);
                float dxRetorno = alvoRetornoX - X;
                float dyRetorno = alvoRetornoY - Y;
                float distanciaRetorno = (float)Math.Sqrt(dxRetorno * dxRetorno + dyRetorno * dyRetorno);

                if (distanciaRetorno > VelocidadeMovimento)
                {
                    X += dxRetorno / distanciaRetorno * VelocidadeMovimento;
                    Y += dyRetorno / distanciaRetorno * VelocidadeMovimento;
                }
                else
                {
                    X = alvoRetornoX;
                    Y = alvoRetornoY;
                    estaVoltandoParaPequenaArea = false;
                }

                AplicarLimites(campo, bolaX);
                return;
            }

            float dx = alvoX - X;
            float dy = alvoY - Y;
            float distancia = (float)Math.Sqrt(dx * dx + dy * dy);

            if (distancia > 2)
            {
                X += (dx / distancia) * VelocidadeMovimento;
                Y += (dy / distancia) * VelocidadeMovimento;
            }

            AplicarLimites(campo, bolaX);
        }

        public void AplicarLimites(RectF campo, float bolaX)
        {
            float limiteMinimoX = campo.X + Raio;
            float limiteMaximoX = campo.X + campo.Width - Raio;
            float limiteMinimoY = campo.Y + Raio;
            float limiteMaximoY = campo.Y + campo.Height - Raio;

            if (Posicao == PosicaoJogador.Goleiro)
            {
                bool bolaNoCampoAdversario = BolaNoCampoAdversario(bolaX, campo);
                if (bolaNoCampoAdversario)
                    estaVoltandoParaPequenaArea = false;
                else if (!estaVoltandoParaPequenaArea && !EstaDentroDaPequenaArea(campo))
                    estaVoltandoParaPequenaArea = true;

                var areaPermitida = bolaNoCampoAdversario
                    || estaVoltandoParaPequenaArea
                    ? CampoFutebol.CalcularGrandeArea(campo, DefendeLadoEsquerdo)
                    : CampoFutebol.CalcularPequenaArea(campo, DefendeLadoEsquerdo);
                limiteMinimoX = areaPermitida.X + Raio;
                limiteMaximoX = areaPermitida.X + areaPermitida.Width - Raio;
                limiteMinimoY = areaPermitida.Y + Raio;
                limiteMaximoY = areaPermitida.Y + areaPermitida.Height - Raio;
            }
            else if (EhDefensor)
            {
                float linhaDoMeio = campo.X + campo.Width / 2;
                if (DefendeLadoEsquerdo)
                    limiteMaximoX = Math.Min(limiteMaximoX, linhaDoMeio - Raio);
                else
                    limiteMinimoX = Math.Max(limiteMinimoX, linhaDoMeio + Raio);
            }

            if (Posicao is PosicaoJogador.LateralEsquerdo or PosicaoJogador.LateralDireito)
            {
                float linhaLateral = campo.Y + campo.Height / 2;
                bool ficaNaFaixaSuperior = Posicao == PosicaoJogador.LateralEsquerdo
                    ? DefendeLadoEsquerdo
                    : !DefendeLadoEsquerdo;

                if (ficaNaFaixaSuperior)
                    limiteMaximoY = Math.Min(limiteMaximoY, linhaLateral - Raio);
                else
                    limiteMinimoY = Math.Max(limiteMinimoY, linhaLateral + Raio);
            }

            X = Math.Clamp(X, limiteMinimoX, limiteMaximoX);
            Y = Math.Clamp(Y, limiteMinimoY, limiteMaximoY);
        }

        private bool BolaNoCampoAdversario(float bolaX, RectF campo)
        {
            float linhaDoMeio = campo.X + campo.Width / 2;
            return DefendeLadoEsquerdo ? bolaX > linhaDoMeio : bolaX < linhaDoMeio;
        }

        private bool EstaDentroDaPequenaArea(RectF campo)
        {
            var area = CampoFutebol.CalcularPequenaArea(campo, DefendeLadoEsquerdo);
            return X >= area.X + Raio &&
                X <= area.X + area.Width - Raio &&
                Y >= area.Y + Raio &&
                Y <= area.Y + area.Height - Raio;
        }

        private bool EhDefensor => Posicao is
            PosicaoJogador.ZagueiroEsquerdo or
            PosicaoJogador.ZagueiroDireito or
            PosicaoJogador.LateralEsquerdo or
            PosicaoJogador.LateralDireito;

        public void ReiniciarPosicao()
        {
            X = posicaoInicialX;
            Y = posicaoInicialY;
            tempoInativoSegundos = 0;
        }

        public void InativarPor(float segundos)
        {
            tempoInativoSegundos = Math.Max(tempoInativoSegundos, segundos);
        }

        public void AtualizarInatividade(float deltaSegundos)
        {
            tempoInativoSegundos = Math.Max(0, tempoInativoSegundos - deltaSegundos);
        }

        public void Desenhar(ICanvas canvas)
        {
            canvas.FillColor = Cor;
            canvas.StrokeColor = Colors.Black;
            canvas.FillEllipse(X - Raio, Y - Raio, Raio * 2, Raio * 2);
            canvas.DrawEllipse(X - Raio, Y - Raio, Raio * 2, Raio * 2);

            canvas.FontColor = Colors.White;
            canvas.FontSize = 9;
            canvas.DrawString($"{AbreviacaoPosicao()} {Habilidade}", X - 24, Y - Raio - 16, 48, 14,
                HorizontalAlignment.Center, VerticalAlignment.Center);
        }

        private string AbreviacaoPosicao() => Posicao switch
        {
            PosicaoJogador.Goleiro => "GK",
            PosicaoJogador.ZagueiroEsquerdo => "ZCE",
            PosicaoJogador.ZagueiroDireito => "ZCD",
            PosicaoJogador.LateralEsquerdo => "LE",
            PosicaoJogador.LateralDireito => "LD",
            PosicaoJogador.MeioCampo => "MID",
            PosicaoJogador.Atacante => "ATT",
            _ => throw new ArgumentOutOfRangeException(nameof(Posicao))
        };
        }
    }