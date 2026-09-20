using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using TiposBarraPeru.Reglas;

namespace TiposBarraPeru.Pruebas
{
    /// <summary>
    /// Comprobaciones de las reglas puras (sin Revit). Cada "Comprobar" cuenta una
    /// comprobacion; al final se imprime cuantas pasan y el codigo de salida es 1
    /// si alguna falla.
    /// </summary>
    internal static class Program
    {
        private static int _ok, _fallos;

        private static void Comprobar(string nombre, bool condicion, string detalle = null)
        {
            if (condicion) { _ok++; return; }
            _fallos++;
            Console.WriteLine("  FALLA: " + nombre + (detalle != null ? "  [" + detalle + "]" : ""));
        }

        private static void Igual(string nombre, double esperado, double real, double tol = 1e-9) =>
            Comprobar(nombre, Math.Abs(esperado - real) <= tol, "esperado " + F(esperado) + ", obtenido " + F(real));

        private static void Igual(string nombre, string esperado, string real) =>
            Comprobar(nombre, esperado == real, "esperado \"" + esperado + "\", obtenido \"" + real + "\"");

        private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        private const string O = "Ø";

        private static int Main()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("Comprobaciones de TiposBarraPeru.Reglas");

            Seccion("1. Catalogo por defecto", PruebasCatalogo);
            Seccion("2. Lectura de config.json", PruebasConfig);
            Seccion("3. Nombres generados", PruebasNombres);
            Seccion("4. Diametros de doblado E.060 (7.2)", PruebasDoblado);
            Seccion("5. Limites de los tramos", PruebasLimites);
            Seccion("6. Extensiones de gancho E.060 (7.1)", PruebasGanchos);
            Seccion("7. Clasificacion de ganchos del proyecto", PruebasClasificacion);
            Seccion("8. Planificador (idempotencia y actualizacion)", PruebasPlanificador);
            Seccion("9. Otros diametros: area, peso y rango de norma", PruebasOtrosDiametros);
            Seccion("10. Otros diametros: parametros E.060 frente a los del catalogo", PruebasParametrosOtros);
            Seccion("11. Otros diametros: catalogoExtra, planificador y validacion", PruebasCatalogoExtra);
            Seccion("12. Concreto: unidades, catalogo y nombres", PruebasConcretoCatalogo);
            Seccion("13. Concreto: modulo de elasticidad y de corte (E.060 8.5)", PruebasConcretoModulos);
            Seccion("14. Concreto: ligero, termico, color y descripcion", PruebasConcretoLigero);
            Seccion("15. Concreto: rango de f'c y densidad", PruebasConcretoRango);
            Seccion("16. Concreto: planificador, extras y validacion", PruebasConcretoPlanificador);
            Seccion("17. Concreto: lectura de materiales.json", PruebasConcretoConfig);

            Console.WriteLine();
            Console.WriteLine(_ok + " comprobaciones superadas, " + _fallos + " fallidas.");
            return _fallos == 0 ? 0 : 1;
        }

        private static void Seccion(string titulo, Action pruebas)
        {
            int antes = _ok + _fallos;
            Console.WriteLine();
            Console.WriteLine(titulo);
            try { pruebas(); }
            catch (Exception ex) { _fallos++; Console.WriteLine("  EXCEPCION: " + ex); }
            Console.WriteLine("  " + (_ok + _fallos - antes) + " comprobaciones");
        }

        // ------------------------------------------------------------------
        private static readonly (string nombre, double mm, double cm2, double kgm)[] Tabla =
        {
            ("6mm", 6.0, 0.28, 0.222), ("8mm", 8.0, 0.50, 0.395), ("3/8\"", 9.5, 0.71, 0.560),
            ("12mm", 12.0, 1.13, 0.888), ("1/2\"", 12.7, 1.29, 0.994), ("5/8\"", 15.9, 2.00, 1.552),
            ("3/4\"", 19.1, 2.84, 2.235), ("1\"", 25.4, 5.10, 3.973), ("1 3/8\"", 35.8, 10.06, 7.907)
        };

        private static void PruebasCatalogo()
        {
            Configuracion cfg = Configuracion.PorDefecto();
            Comprobar("9 tamanos", cfg.Catalogo.Count == 9, cfg.Catalogo.Count.ToString());
            for (int i = 0; i < Tabla.Length && i < cfg.Catalogo.Count; i++)
            {
                BarraCatalogo b = cfg.Catalogo[i];
                Igual("nombre " + Tabla[i].nombre, Tabla[i].nombre, b.Nombre);
                Igual("diametro " + Tabla[i].nombre, Tabla[i].mm, b.DiametroMm);
                Igual("area " + Tabla[i].nombre, Tabla[i].cm2, b.AreaCm2);
                Igual("peso " + Tabla[i].nombre, Tabla[i].kgm, b.PesoKgM);
                Comprobar("corrugada " + Tabla[i].nombre, b.Corrugada);
            }
            Igual("prefijo por defecto", O, cfg.PrefijoNombre);
            Comprobar("actualizar existentes desactivado", !cfg.ActualizarExistentes);
            Comprobar("ganchos automaticos por defecto", !cfg.LongitudesGanchoSegunE060);
            Igual("radio maximo de doblado", 10000, cfg.RadioMaximoDobladoMm);
        }

        private static void PruebasConfig()
        {
            // a) el config.json distribuido con el add-in equivale a la configuracion por defecto
            string ruta = Path.Combine(AppContext.BaseDirectory, "config.json");
            Comprobar("config.json copiado junto a las pruebas", File.Exists(ruta), ruta);
            Configuracion leida = Configuracion.Cargar(ruta);
            Comprobar("config.json: 9 tamanos", leida.Catalogo.Count == 9);
            Igual("config.json equivale al por defecto", Configuracion.PorDefecto().Serializar(), leida.Serializar());

            // b) comentarios, comas finales y camelCase
            string json = "{\n  // comentario\n  \"prefijoNombre\": \"\",\n  \"actualizarExistentes\": true,\n" +
                          "  \"catalogo\": [ { \"nombre\": \"1/2\\\"\", \"diametroMm\": 12.7, \"areaCm2\": 1.29, \"pesoKgM\": 0.994, \"corrugada\": false }, ],\n" +
                          "  \"reglasE060\": { \"ganchos\": { \"estribo135\": { \"multiplicador\": 6, \"minimoMm\": 75, }, }, },\n}\n";
            Configuracion c = Configuracion.Deserializar(json);
            Igual("prefijo vacio leido", "", c.PrefijoNombre);
            Comprobar("actualizarExistentes leido", c.ActualizarExistentes);
            Comprobar("catalogo de 1 entrada", c.Catalogo.Count == 1);
            Comprobar("corrugada=false leido", c.Catalogo.Count == 1 && !c.Catalogo[0].Corrugada);
            Igual("nombre con comilla escapada", "1/2\"", c.Catalogo[0].Nombre);
            Igual("estribo135 minimo 75 leido", 75, c.ReglasE060.Ganchos.Estribo135.MinimoMm);
            Comprobar("tablas no indicadas toman el valor por defecto", c.ReglasE060.DobladoBarras.Count == 3 && c.ReglasE060.DobladoEstribos.Count == 2);

            // c) archivo inexistente y JSON vacio -> por defecto
            Configuracion sinArchivo = Configuracion.Cargar(Path.Combine(AppContext.BaseDirectory, "no_existe.json"));
            Comprobar("sin archivo: catalogo por defecto", sinArchivo.Catalogo.Count == 9);
            Configuracion vacio = Configuracion.Deserializar("{}");
            Comprobar("JSON vacio: catalogo por defecto", vacio.Catalogo.Count == 9);
            Igual("JSON vacio: prefijo por defecto", O, vacio.PrefijoNombre);

            // d) ida y vuelta
            Configuracion clon = leida.Clonar();
            Igual("clon identico", leida.Serializar(), clon.Serializar());
            Comprobar("serializado legible (prefijo sin escapar)", leida.Serializar().Contains("\"prefijoNombre\": \"" + O + "\""));

            // e) JSON invalido lanza excepcion
            bool lanza = false;
            try { Configuracion.Deserializar("{ esto no es json"); } catch { lanza = true; }
            Comprobar("JSON invalido lanza excepcion", lanza);
        }

        private static void PruebasNombres()
        {
            string[] esperados = { O + "6mm", O + "8mm", O + "3/8\"", O + "12mm", O + "1/2\"", O + "5/8\"", O + "3/4\"", O + "1\"", O + "1 3/8\"" };
            Configuracion cfg = Configuracion.PorDefecto();
            for (int i = 0; i < esperados.Length; i++)
                Igual("nombre " + esperados[i], esperados[i], Nombres.Generar(cfg.PrefijoNombre, cfg.Catalogo[i].Nombre, cfg.SimboloPulgada));

            Igual("prefijo vacio", "3/8\"", Nombres.Generar("", "3/8\""));
            Igual("prefijo nulo", "12mm", Nombres.Generar(null, "12mm"));
            Igual("prefijo personalizado", "D-1\"", Nombres.Generar("D-", "1\""));
            Igual("simbolo de pulgada alternativo", O + "1 3/8in", Nombres.Generar(O, "1 3/8\"", "in"));
            Igual("simbolo de pulgada alternativo no afecta a mm", O + "6mm", Nombres.Generar(O, "6mm", "in"));
            Igual("espacios en el nombre de catalogo", O + "1\"", Nombres.Generar(O, "  1\" "));
            Comprobar("comparacion sin mayusculas", Nombres.Iguales("ø12mm", O + "12mm"));
            Comprobar("comparacion con espacios", Nombres.Iguales(" " + O + "12mm ", O + "12mm"));
            Comprobar("comparacion distinta", !Nombres.Iguales(O + "12mm", O + "1/2\""));
        }

        private static void PruebasDoblado()
        {
            var r = new ReglasE060(Configuracion.PorDefecto().ReglasE060);
            double[] barra = { 36, 48, 57, 72, 76.2, 95.4, 114.6, 152.4, 286.4 };
            double[] estribo = { 24, 32, 38, 48, 50.8, 63.6, 114.6, 152.4, 214.8 };
            for (int i = 0; i < Tabla.Length; i++)
            {
                double db = Tabla[i].mm;
                Igual("doblado barra " + Tabla[i].nombre, barra[i], r.DiametroDobladoBarraMm(db), 1e-9);
                Igual("doblado gancho " + Tabla[i].nombre, barra[i], r.DiametroDobladoGanchoMm(db), 1e-9);
                Igual("doblado estribo " + Tabla[i].nombre, estribo[i], r.DiametroDobladoEstriboMm(db), 1e-9);
            }
            ValoresE060 v = r.Calcular(35.8);
            Igual("Calcular: multiplicador barra 1 3/8\"", 8, v.MultiplicadorBarra);
            Igual("Calcular: multiplicador estribo 1 3/8\"", 6, v.MultiplicadorEstribo);
            Igual("Calcular: doblado barra 1 3/8\"", 286.4, v.DobladoBarraMm, 1e-9);
        }

        private static void PruebasLimites()
        {
            var r = new ReglasE060(Configuracion.PorDefecto().ReglasE060);
            Igual("estribo 15.9 -> 4 db", 4, r.MultiplicadorDobladoEstribo(15.9));
            Igual("estribo 15.9000001 -> 4 db (tolerancia)", 4, r.MultiplicadorDobladoEstribo(15.9000001));
            Igual("estribo 16.0 -> 6 db", 6, r.MultiplicadorDobladoEstribo(16.0));
            Igual("barra 25.4 -> 6 db", 6, r.MultiplicadorDobladoBarra(25.4));
            Igual("barra 25.5 -> 8 db", 8, r.MultiplicadorDobladoBarra(25.5));
            Igual("barra 35.8 -> 8 db", 8, r.MultiplicadorDobladoBarra(35.8));
            Igual("barra 36 -> 10 db", 10, r.MultiplicadorDobladoBarra(36));
            Igual("barra 57 (2 1/4\") -> 10 db", 10, r.MultiplicadorDobladoBarra(57));
            Igual("barra 6 -> 6 db", 6, r.MultiplicadorDobladoBarra(6));
            Igual("estribo 6 -> 4 db", 4, r.MultiplicadorDobladoEstribo(6));

            // tabla desordenada en el config: Normalizar la ordena
            var cfg = Configuracion.Deserializar("{ \"reglasE060\": { \"dobladoBarras\": [ { \"hastaDiametroMm\": 999, \"multiplicador\": 10 }, { \"hastaDiametroMm\": 25.4, \"multiplicador\": 6 } ] } }");
            var r2 = new ReglasE060(cfg.ReglasE060);
            Igual("tabla desordenada: 12 -> 6 db", 6, r2.MultiplicadorDobladoBarra(12));
            Igual("tabla desordenada: 30 -> 10 db", 10, r2.MultiplicadorDobladoBarra(30));
            Igual("por encima del ultimo tramo se usa el ultimo", 10, r2.MultiplicadorDobladoBarra(5000));
        }

        private static void PruebasGanchos()
        {
            var r = new ReglasE060(Configuracion.PorDefecto().ReglasE060);
            // 180 grados: 4 db, minimo 65 mm
            Igual("180: 6mm -> 65 (minimo)", 65, r.ExtensionGanchoMm(TipoGancho.Estandar180, 6));
            Igual("180: 12mm -> 65 (minimo)", 65, r.ExtensionGanchoMm(TipoGancho.Estandar180, 12));
            Igual("180: 5/8\" -> 65 (63.6 < 65)", 65, r.ExtensionGanchoMm(TipoGancho.Estandar180, 15.9));
            Igual("180: 3/4\" -> 76.4", 76.4, r.ExtensionGanchoMm(TipoGancho.Estandar180, 19.1), 1e-9);
            Igual("180: 1\" -> 101.6", 101.6, r.ExtensionGanchoMm(TipoGancho.Estandar180, 25.4), 1e-9);
            Igual("180: 1 3/8\" -> 143.2", 143.2, r.ExtensionGanchoMm(TipoGancho.Estandar180, 35.8), 1e-9);
            // 90 grados: 12 db
            Igual("90: 6mm -> 72", 72, r.ExtensionGanchoMm(TipoGancho.Estandar90, 6));
            Igual("90: 12mm -> 144", 144, r.ExtensionGanchoMm(TipoGancho.Estandar90, 12));
            Igual("90: 1\" -> 304.8", 304.8, r.ExtensionGanchoMm(TipoGancho.Estandar90, 25.4), 1e-9);
            // estribo 90: 6 db hasta 5/8", 12 db para 3/4" y 1"
            Igual("estribo 90: 6mm -> 36", 36, r.ExtensionGanchoMm(TipoGancho.Estribo90, 6));
            Igual("estribo 90: 3/8\" -> 57", 57, r.ExtensionGanchoMm(TipoGancho.Estribo90, 9.5));
            Igual("estribo 90: 5/8\" -> 95.4", 95.4, r.ExtensionGanchoMm(TipoGancho.Estribo90, 15.9), 1e-9);
            Igual("estribo 90: 3/4\" -> 229.2", 229.2, r.ExtensionGanchoMm(TipoGancho.Estribo90, 19.1), 1e-9);
            Igual("estribo 90: 1\" -> 304.8", 304.8, r.ExtensionGanchoMm(TipoGancho.Estribo90, 25.4), 1e-9);
            // estribo 135: 6 db
            Igual("estribo 135: 3/8\" -> 57", 57, r.ExtensionGanchoMm(TipoGancho.Estribo135, 9.5));
            Igual("estribo 135: 1/2\" -> 76.2", 76.2, r.ExtensionGanchoMm(TipoGancho.Estribo135, 12.7), 1e-9);
            // gancho sismico: minimo 75 mm
            var cfg = Configuracion.PorDefecto();
            cfg.ReglasE060.Ganchos.Estribo135.MinimoMm = 75;
            var rs = new ReglasE060(cfg.ReglasE060);
            Igual("estribo 135 sismico: 3/8\" -> 75", 75, rs.ExtensionGanchoMm(TipoGancho.Estribo135, 9.5));
            Igual("estribo 135 sismico: 1/2\" -> 76.2", 76.2, rs.ExtensionGanchoMm(TipoGancho.Estribo135, 12.7), 1e-9);

            Comprobar("descripcion 180", r.Describir(TipoGancho.Estandar180) == "180 grados: 4 db (min. 65 mm)", r.Describir(TipoGancho.Estandar180));
            Comprobar("descripcion estribo 90", r.Describir(TipoGancho.Estribo90) == "estribo 90 grados: 6 db hasta 15.9 mm, 12 db por encima", r.Describir(TipoGancho.Estribo90));
        }

        private static void PruebasClasificacion()
        {
            Comprobar("estandar 180", ReglasE060.Clasificar(false, 180) == TipoGancho.Estandar180);
            Comprobar("estandar 90", ReglasE060.Clasificar(false, 90) == TipoGancho.Estandar90);
            Comprobar("estandar 135 -> sin regla", ReglasE060.Clasificar(false, 135) == null);
            Comprobar("estribo 90", ReglasE060.Clasificar(true, 90) == TipoGancho.Estribo90);
            Comprobar("estribo 135", ReglasE060.Clasificar(true, 135) == TipoGancho.Estribo135);
            Comprobar("estribo 180 -> regla de 135", ReglasE060.Clasificar(true, 180) == TipoGancho.Estribo135);
            Comprobar("tolerancia de angulo (89.5)", ReglasE060.Clasificar(true, 89.5) == TipoGancho.Estribo90);
            Comprobar("angulo raro (45) -> sin regla", ReglasE060.Clasificar(true, 45) == null);
            Comprobar("angulo en radianes convertido por el add-in: 180 grados", Math.Abs(Math.PI * 180.0 / Math.PI - 180) < 1e-12);
        }

        private static void PruebasPlanificador()
        {
            Configuracion cfg = Configuracion.PorDefecto();
            var existentes = new List<TipoExistente>
            {
                new TipoExistente(O + "12mm", 12.0),          // igual
                new TipoExistente("ø1/2\"", 12.7),        // solo cambia la mayuscula del prefijo
                new TipoExistente(O + "8mm", 10.0),           // mismo nombre, otro diametro
                new TipoExistente("16M", 16.0)                // ajeno al catalogo
            };

            // a) sin actualizar: idempotente, solo se crean los que faltan
            List<FilaPlan> plan = Planificador.Planificar(cfg, O, existentes, false);
            Comprobar("9 filas", plan.Count == 9);
            FilaPlan F12 = plan.First(f => f.Barra.Nombre == "12mm");
            FilaPlan F12p = plan.First(f => f.Barra.Nombre == "1/2\"");
            FilaPlan F8 = plan.First(f => f.Barra.Nombre == "8mm");
            FilaPlan F6 = plan.First(f => f.Barra.Nombre == "6mm");
            Comprobar("12mm ya existe", F12.Estado == EstadoFila.YaExiste && !F12.Crear);
            Comprobar("1/2\" ya existe (sin distinguir mayusculas)", F12p.Estado == EstadoFila.YaExiste && !F12p.Crear);
            Comprobar("8mm ya existe con aviso de diametro", F8.Estado == EstadoFila.YaExiste && F8.Aviso == "con diametro 10 mm", F8.Aviso);
            Comprobar("6mm se creara", F6.Estado == EstadoFila.SeCreara && F6.Crear);
            Comprobar("6 filas a crear", plan.Count(f => f.Crear) == 6);
            Comprobar("texto de estado", F8.TextoEstado == "ya existe (con diametro 10 mm)", F8.TextoEstado);
            Comprobar("existentes no seleccionables", !F12.Seleccionable && F6.Seleccionable);
            Comprobar("valores E.060 en la fila", Math.Abs(F6.Valores.DobladoEstriboMm - 24) < 1e-9);

            // b) actualizar existentes
            plan = Planificador.Planificar(cfg, O, existentes, true);
            Comprobar("actualizar: 12mm se actualizara", plan.First(f => f.Barra.Nombre == "12mm").Estado == EstadoFila.SeActualizara);
            Comprobar("actualizar: 9 filas marcadas", plan.Count(f => f.Crear) == 9);
            Comprobar("actualizar: aviso de diametro se conserva", plan.First(f => f.Barra.Nombre == "8mm").Aviso == "con diametro 10 mm");

            // c) otro prefijo: nada existe
            plan = Planificador.Planificar(cfg, "D", existentes, false);
            Comprobar("prefijo D: todo se creara", plan.All(f => f.Estado == EstadoFila.SeCreara) && plan[0].Nombre == "D6mm");

            // d) validador de nombres de Revit (simulado: rechaza la comilla)
            plan = Planificador.Planificar(cfg, O, existentes, false, n => !n.Contains("\""));
            Comprobar("nombres con comilla no validos", plan.Count(f => f.Estado == EstadoFila.NombreInvalido) == 6);
            Comprobar("nombre no valido no se marca", plan.Where(f => f.Estado == EstadoFila.NombreInvalido).All(f => !f.Crear && !f.Seleccionable));
            cfg.SimboloPulgada = "in";
            plan = Planificador.Planificar(cfg, O, existentes, false, n => !n.Contains("\""));
            Comprobar("con simboloPulgada=in todos los nombres son validos", plan.All(f => f.Estado != EstadoFila.NombreInvalido));
            Comprobar("con simboloPulgada=in 1/2 se llama " + O + "1/2in", plan.Any(f => f.Nombre == O + "1/2in"));

            // e) sin tipos existentes
            plan = Planificador.Planificar(cfg, O, null, false);
            Comprobar("sin existentes: 9 a crear", plan.Count(f => f.Crear) == 9);
        }

        // ------------------------------------------------------------------
        private static void PruebasOtrosDiametros()
        {
            // formulas: area = pi d^2 / 4 (cm2), peso = area * 0.785 (kg/m)
            Igual("area 10 mm", 0.7854, PropiedadesBarra.AreaCm2(10), 1e-4);
            Igual("peso 10 mm", 0.6165, PropiedadesBarra.PesoKgMDesdeDiametro(10), 1e-4);
            Igual("area 1 1/4\" (31.8)", 7.9423, PropiedadesBarra.AreaCm2(31.8), 1e-4);
            Igual("peso 1 1/4\" (31.8)", 6.2347, PropiedadesBarra.PesoKgMDesdeDiametro(31.8), 1e-4);
            Igual("area #6 (19.05)", 2.8502, PropiedadesBarra.AreaCm2(19.05), 1e-4);
            Igual("peso #6 (19.05)", 2.2374, PropiedadesBarra.PesoKgMDesdeDiametro(19.05), 1e-4);
            Igual("area 5 mm (se calcula aunque este fuera de norma)", 0.1963, PropiedadesBarra.AreaCm2(5), 1e-4);
            Igual("area 60 mm", 28.2743, PropiedadesBarra.AreaCm2(60), 1e-4);
            Igual("peso desde area: 1 cm2 -> 0.785 kg/m", 0.785, PropiedadesBarra.PesoKgM(1.0), 1e-9);
            Igual("redondeo area 10 mm", 0.79, PropiedadesBarra.RedondearArea(PropiedadesBarra.AreaCm2(10)), 1e-9);
            Igual("redondeo peso 10 mm", 0.617, PropiedadesBarra.RedondearPeso(PropiedadesBarra.PesoKgMDesdeDiametro(10)), 1e-9);
            Igual("diametro 0 -> area 0", 0, PropiedadesBarra.AreaCm2(0));

            // las formulas reproducen el catalogo (los valores del fabricante difieren < 3 %)
            foreach (var t in Tabla)
            {
                double a = PropiedadesBarra.AreaCm2(t.mm), w = PropiedadesBarra.PesoKgMDesdeDiametro(t.mm);
                Comprobar("area formula vs catalogo " + t.nombre, Math.Abs(a - t.cm2) / t.cm2 < 0.03, F(a) + " vs " + F(t.cm2));
                Comprobar("peso formula vs catalogo " + t.nombre, Math.Abs(w - t.kgm) / t.kgm < 0.03, F(w) + " vs " + F(t.kgm));
            }

            // rango de la norma: 6 a 57 mm (config)
            var r = new ReglasE060(Configuracion.PorDefecto().ReglasE060);
            Igual("minimo de norma", 6, r.DiametroMinimoMm);
            Igual("maximo de norma", 57, r.DiametroMaximoMm);
            Comprobar("10 mm dentro", r.DentroDeNorma(10) && r.MotivoFueraDeNorma(10) == null);
            Comprobar("31.8 dentro", r.DentroDeNorma(31.8));
            Comprobar("19.05 dentro", r.DentroDeNorma(19.05));
            Comprobar("6 dentro (limite)", r.DentroDeNorma(6));
            Comprobar("57 dentro (limite)", r.DentroDeNorma(57));
            Comprobar("5 fuera", !r.DentroDeNorma(5));
            Comprobar("5.99 fuera", !r.DentroDeNorma(5.99));
            Comprobar("60 fuera", !r.DentroDeNorma(60));
            Comprobar("57.01 fuera", !r.DentroDeNorma(57.01));
            Igual("motivo 5 mm", "menor de 6 mm, fuera de la norma", r.MotivoFueraDeNorma(5));
            Igual("motivo 60 mm", "mayor de 57 mm, fuera de la norma", r.MotivoFueraDeNorma(60));
            Igual("motivo 0 mm", "diametro no valido", r.MotivoFueraDeNorma(0));
            Comprobar("Calcular.DentroDeNorma", r.Calcular(10).DentroDeNorma && !r.Calcular(60).DentroDeNorma);

            // rango editable en config
            var cfg = Configuracion.Deserializar("{ \"reglasE060\": { \"diametroMinimoNormaMm\": 8, \"diametroMaximoNormaMm\": 40 } }");
            var r2 = new ReglasE060(cfg.ReglasE060);
            Comprobar("rango de config: 6 fuera, 8 dentro, 40 dentro, 41 fuera",
                      !r2.DentroDeNorma(6) && r2.DentroDeNorma(8) && r2.DentroDeNorma(40) && !r2.DentroDeNorma(41));
            var mal = Configuracion.Deserializar("{ \"reglasE060\": { \"diametroMinimoNormaMm\": 0, \"diametroMaximoNormaMm\": -1 } }");
            Comprobar("rango invalido en config vuelve al de por defecto",
                      mal.ReglasE060.DiametroMinimoNormaMm == 6 && mal.ReglasE060.DiametroMaximoNormaMm == 57);
        }

        /// <summary>
        /// Un diametro nuevo recibe exactamente los mismos multiplicadores que los tamanos
        /// del catalogo que lo rodean, y los valores absolutos escalan con su diametro.
        /// </summary>
        private static void PruebasParametrosOtros()
        {
            var r = new ReglasE060(Configuracion.PorDefecto().ReglasE060);

            // 10 mm: entre 3/8" (9.5) y 12 mm -> 6 db barra, 4 db estribo, estribo 90 6 db
            ValoresE060 v10 = r.Calcular(10), v95 = r.Calcular(9.5), v12 = r.Calcular(12);
            Comprobar("10 mm: multiplicador barra como 3/8\" y 12mm", v10.MultiplicadorBarra == v95.MultiplicadorBarra && v10.MultiplicadorBarra == v12.MultiplicadorBarra && v10.MultiplicadorBarra == 6);
            Comprobar("10 mm: multiplicador estribo como 3/8\" y 12mm", v10.MultiplicadorEstribo == 4 && v95.MultiplicadorEstribo == 4 && v12.MultiplicadorEstribo == 4);
            Igual("10 mm: doblado barra 60", 60, v10.DobladoBarraMm, 1e-9);
            Igual("10 mm: doblado gancho 60", 60, v10.DobladoGanchoMm, 1e-9);
            Igual("10 mm: doblado estribo 40", 40, v10.DobladoEstriboMm, 1e-9);
            Igual("10 mm: gancho 180 -> 65 (minimo, como 3/8\" y 12mm)", 65, v10.GanchoEstandar180Mm);
            Igual("10 mm: gancho 90 -> 120", 120, v10.GanchoEstandar90Mm, 1e-9);
            Igual("10 mm: estribo 90 -> 60", 60, v10.GanchoEstribo90Mm, 1e-9);
            Igual("10 mm: estribo 135 -> 60", 60, v10.GanchoEstribo135Mm, 1e-9);
            Comprobar("10 mm: doblado entre los de 3/8\" y 12mm", v95.DobladoBarraMm < v10.DobladoBarraMm && v10.DobladoBarraMm < v12.DobladoBarraMm);

            // #6 = 19.05 mm: como 3/4" (19.1) -> 6 db barra, 6 db estribo, estribo 90 12 db
            ValoresE060 v6 = r.Calcular(19.05), v34 = r.Calcular(19.1), v58 = r.Calcular(15.9);
            Comprobar("#6: multiplicadores como 3/4\"", v6.MultiplicadorBarra == v34.MultiplicadorBarra && v6.MultiplicadorEstribo == v34.MultiplicadorEstribo);
            Igual("#6: doblado barra 114.3", 114.3, v6.DobladoBarraMm, 1e-9);
            Igual("#6: doblado estribo 114.3 (6 db, ya no 4 db)", 114.3, v6.DobladoEstriboMm, 1e-9);
            Comprobar("#6: estribo 6 db mientras 5/8\" sigue en 4 db", v58.MultiplicadorEstribo == 4 && v6.MultiplicadorEstribo == 6);
            Igual("#6: gancho 180 -> 76.2 (4 db > 65)", 76.2, v6.GanchoEstandar180Mm, 1e-9);
            Igual("#6: gancho 90 -> 228.6", 228.6, v6.GanchoEstandar90Mm, 1e-9);
            Igual("#6: estribo 90 -> 228.6 (12 db como 3/4\")", 228.6, v6.GanchoEstribo90Mm, 1e-9);
            Igual("#6: estribo 135 -> 114.3", 114.3, v6.GanchoEstribo135Mm, 1e-9);
            Comprobar("#6 y 3/4\": misma regla de estribo 90", Math.Abs(v34.GanchoEstribo90Mm - 12 * 19.1) < 1e-9);

            // 1 1/4" = 31.8 mm: entre 1" (25.4) y 1 3/8" (35.8) -> 8 db barra como 1 3/8", 6 db estribo como 1"
            ValoresE060 v114 = r.Calcular(31.8), v1 = r.Calcular(25.4), v138 = r.Calcular(35.8);
            Comprobar("1 1/4\": barra 8 db como 1 3/8\" (y no 6 db como 1\")", v114.MultiplicadorBarra == 8 && v138.MultiplicadorBarra == 8 && v1.MultiplicadorBarra == 6);
            Comprobar("1 1/4\": estribo 6 db como 1\" y 1 3/8\"", v114.MultiplicadorEstribo == 6 && v1.MultiplicadorEstribo == 6 && v138.MultiplicadorEstribo == 6);
            Igual("1 1/4\": doblado barra 254.4", 254.4, v114.DobladoBarraMm, 1e-9);
            Igual("1 1/4\": doblado estribo 190.8", 190.8, v114.DobladoEstriboMm, 1e-9);
            Igual("1 1/4\": gancho 180 -> 127.2", 127.2, v114.GanchoEstandar180Mm, 1e-9);
            Igual("1 1/4\": gancho 90 -> 381.6", 381.6, v114.GanchoEstandar90Mm, 1e-9);
            Igual("1 1/4\": estribo 90 -> 381.6 (12 db)", 381.6, v114.GanchoEstribo90Mm, 1e-9);
            Comprobar("1 1/4\": doblado entre los de 1\" y 1 3/8\"", v1.DobladoBarraMm < v114.DobladoBarraMm && v114.DobladoBarraMm < v138.DobladoBarraMm);

            // fuera de rango: las reglas se siguen evaluando (para mostrarlas) pero DentroDeNorma es false
            ValoresE060 v5 = r.Calcular(5), v60 = r.Calcular(60);
            Comprobar("5 mm: fuera de norma, 6 db / 4 db", !v5.DentroDeNorma && v5.MultiplicadorBarra == 6 && v5.MultiplicadorEstribo == 4);
            Comprobar("60 mm: fuera de norma, 10 db / 6 db", !v60.DentroDeNorma && v60.MultiplicadorBarra == 10 && v60.MultiplicadorEstribo == 6);
            Igual("5 mm: gancho 180 -> 65 (minimo)", 65, v5.GanchoEstandar180Mm);

            // el catalogo original sale igual por Calcular que por las funciones sueltas
            foreach (var t in Tabla)
            {
                ValoresE060 v = r.Calcular(t.mm);
                Comprobar("Calcular coherente " + t.nombre,
                          Math.Abs(v.DobladoBarraMm - r.DiametroDobladoBarraMm(t.mm)) < 1e-9 &&
                          Math.Abs(v.GanchoEstribo90Mm - r.ExtensionGanchoMm(TipoGancho.Estribo90, t.mm)) < 1e-9 && v.DentroDeNorma);
            }
        }

        private static void PruebasCatalogoExtra()
        {
            // a) config con extras: lectura, limpieza de duplicados y de entradas vacias
            string json = "{ \"catalogoExtra\": [" +
                          " { \"nombre\": \"10mm\", \"diametroMm\": 10, \"areaCm2\": 0.79, \"pesoKgM\": 0.617 }," +
                          " { \"nombre\": \"1 1/4\\\"\", \"diametroMm\": 31.8, \"areaCm2\": 7.94, \"pesoKgM\": 6.235 }," +
                          " { \"nombre\": \"#6\", \"diametroMm\": 19.05, \"areaCm2\": 2.85, \"pesoKgM\": 2.237 }," +
                          " { \"nombre\": \"5mm\", \"diametroMm\": 5 }," +
                          " { \"nombre\": \"60mm\", \"diametroMm\": 60 }," +
                          " { \"nombre\": \"12mm\", \"diametroMm\": 12 }," +
                          " { \"nombre\": \"10MM\", \"diametroMm\": 10 }," +
                          " { \"nombre\": \"\", \"diametroMm\": 14 }," +
                          " { \"nombre\": \"sin diametro\" } ] }";
            Configuracion cfg = Configuracion.Deserializar(json);
            Comprobar("catalogo original intacto (9)", cfg.Catalogo.Count == 9);
            Comprobar("extras: 5 validos (se quitan el repetido del catalogo, el repetido entre extras, el vacio y el sin diametro)",
                      cfg.CatalogoExtra.Count == 5, cfg.CatalogoExtra.Count.ToString());
            Comprobar("extras: 12mm no se duplica", !cfg.CatalogoExtra.Any(b => b.Nombre == "12mm"));
            Comprobar("extras: 10MM (repetido sin mayusculas) se descarta", cfg.CatalogoExtra.Count(b => Nombres.Iguales(b.Nombre, "10mm")) == 1);
            Comprobar("extras corrugada por defecto", cfg.CatalogoExtra.All(b => b.Corrugada));
            Comprobar("ida y vuelta conserva los extras", Configuracion.Deserializar(cfg.Serializar()).CatalogoExtra.Count == 5);
            Comprobar("config por defecto sin extras", Configuracion.PorDefecto().CatalogoExtra.Count == 0);

            // b) planificador: 9 + 5 filas, extras marcados, fuera de norma no se crean
            var existentes = new List<TipoExistente> { new TipoExistente(O + "#6", 19.05) };
            List<FilaPlan> plan = Planificador.Planificar(cfg, O, existentes, false);
            Comprobar("14 filas", plan.Count == 14, plan.Count.ToString());
            Comprobar("las 9 primeras no son extra y las 5 ultimas si", plan.Take(9).All(f => !f.EsExtra) && plan.Skip(9).All(f => f.EsExtra));
            FilaPlan f10 = plan.First(f => f.Barra.Nombre == "10mm");
            FilaPlan f114 = plan.First(f => f.Barra.Nombre == "1 1/4\"");
            FilaPlan f6 = plan.First(f => f.Barra.Nombre == "#6");
            FilaPlan f5 = plan.First(f => f.Barra.Nombre == "5mm");
            FilaPlan f60 = plan.First(f => f.Barra.Nombre == "60mm");
            Comprobar("10mm se creara con nombre " + O + "10mm", f10.Estado == EstadoFila.SeCreara && f10.Crear && f10.Nombre == O + "10mm");
            Comprobar("1 1/4\" se creara", f114.Estado == EstadoFila.SeCreara && f114.Nombre == O + "1 1/4\"");
            Comprobar("#6 ya existe en el proyecto", f6.Estado == EstadoFila.YaExiste && !f6.Crear);
            Comprobar("5mm fuera de norma: no se crea", f5.Estado == EstadoFila.FueraDeNorma && !f5.Crear && !f5.Seleccionable);
            Igual("5mm texto de estado", "no se crea (menor de 6 mm, fuera de la norma)", f5.TextoEstado);
            Comprobar("60mm fuera de norma: no se crea", f60.Estado == EstadoFila.FueraDeNorma && !f60.Crear);
            Igual("60mm texto de estado", "no se crea (mayor de 57 mm, fuera de la norma)", f60.TextoEstado);
            Comprobar("fuera de norma tampoco con actualizar", Planificador.Planificar(cfg, O, existentes, true).Count(f => f.Estado == EstadoFila.FueraDeNorma) == 2);
            Comprobar("11 filas a crear (9 del catalogo + 5 extras - #6 existente - 2 fuera de norma)", plan.Count(f => f.Crear) == 11, plan.Count(f => f.Crear).ToString());
            Comprobar("valores E.060 del extra 1 1/4\"", Math.Abs(f114.Valores.DobladoBarraMm - 254.4) < 1e-9 && Math.Abs(f114.Valores.GanchoEstandar180Mm - 127.2) < 1e-9);

            // c) nombre repetido en la tabla (extra que, con simboloPulgada, coincide con uno del catalogo)
            Configuracion cfg2 = Configuracion.PorDefecto();
            cfg2.SimboloPulgada = "in";
            cfg2.CatalogoExtra.Add(new BarraCatalogo { Nombre = "1/2in", DiametroMm = 12.7 });
            List<FilaPlan> plan2 = Planificador.Planificar(cfg2, O, null, false);
            Comprobar("dos filas producen " + O + "1/2in: ambas marcadas como repetidas",
                      plan2.Count(f => f.Estado == EstadoFila.NombreDuplicado) == 2 && plan2.Where(f => f.Estado == EstadoFila.NombreDuplicado).All(f => !f.Crear));
            Igual("texto de estado repetido", "nombre repetido en la tabla", plan2.First(f => f.Estado == EstadoFila.NombreDuplicado).TextoEstado);

            // d) validacion de una barra nueva antes de anadirla
            Configuracion cfg3 = Configuracion.PorDefecto();
            cfg3.CatalogoExtra.Add(new BarraCatalogo { Nombre = "10mm", DiametroMm = 10 });
            var proyecto = new List<TipoExistente> { new TipoExistente(O + "#6", 19.05), new TipoExistente("16M", 16) };
            Func<string, bool> valido = n => !n.Contains("|");
            Comprobar("valida: 1 1/4\" 31.8", Planificador.ValidarNuevaBarra(cfg3, O, "1 1/4\"", 31.8, proyecto, valido) == null);
            Comprobar("valida: #5 15.875", Planificador.ValidarNuevaBarra(cfg3, O, "#5", 15.875, proyecto, valido) == null);
            Comprobar("valida: nombre con espacios alrededor", Planificador.ValidarNuevaBarra(cfg3, O, "  14mm ", 14, proyecto, valido) == null);
            Comprobar("rechaza: nombre vacio", Planificador.ValidarNuevaBarra(cfg3, O, "  ", 14, proyecto, valido) != null);
            Igual("rechaza: 5 mm", "diametro menor de 6 mm, fuera de la norma", Planificador.ValidarNuevaBarra(cfg3, O, "5mm", 5, proyecto, valido));
            Igual("rechaza: 60 mm", "diametro mayor de 57 mm, fuera de la norma", Planificador.ValidarNuevaBarra(cfg3, O, "60mm", 60, proyecto, valido));
            Igual("rechaza: repetido en catalogo", "\"12mm\" ya esta en el catalogo", Planificador.ValidarNuevaBarra(cfg3, O, "12mm", 12, proyecto, valido));
            Igual("rechaza: repetido en catalogo sin mayusculas", "\"12MM\" ya esta en el catalogo", Planificador.ValidarNuevaBarra(cfg3, O, "12MM", 12, proyecto, valido));
            Igual("rechaza: repetido en extras", "\"10mm\" ya esta en la lista", Planificador.ValidarNuevaBarra(cfg3, O, "10mm", 10, proyecto, valido));
            Igual("rechaza: choca con tipo del proyecto", "el proyecto ya tiene un tipo \"" + O + "#6\"", Planificador.ValidarNuevaBarra(cfg3, O, "#6", 19.05, proyecto, valido));
            Comprobar("con otro prefijo #6 ya no choca", Planificador.ValidarNuevaBarra(cfg3, "D", "#6", 19.05, proyecto, valido) == null);
            Comprobar("rechaza: nombre no admitido por Revit", (Planificador.ValidarNuevaBarra(cfg3, O, "a|b", 10.5, proyecto, valido) ?? "").StartsWith("Revit no admite"));
            Comprobar("el mismo diametro con otro nombre es valido (14mm y #4.4)", Planificador.ValidarNuevaBarra(cfg3, O, "otro10", 10, proyecto, valido) == null);
            Comprobar("sin existentes ni validador", Planificador.ValidarNuevaBarra(cfg3, O, "20mm", 20, null) == null);
        }

        // ------------------------------------------------------------------
        // Materiales de concreto
        // ------------------------------------------------------------------
        private const string P = "Concreto f'c ";
        private static readonly double[] Fcs = { 140, 175, 210, 245, 280, 315, 350, 420 };

        private static void PruebasConcretoCatalogo()
        {
            // unidades: 1 kg/cm2 = 0.0980665 MPa
            Igual("210 kg/cm2 -> 20.594 MPa", 20.594, Unidades.KgCm2AMPa(210), 1e-3);
            Igual("280 kg/cm2 -> 27.459 MPa", 27.459, Unidades.KgCm2AMPa(280), 1e-3);
            Igual("21316.78 MPa -> 217370.65 kg/cm2", 217370.65, Unidades.MPaAKgCm2(21316.78), 0.1);
            Igual("ida y vuelta kg/cm2 -> MPa -> kg/cm2", 350, Unidades.MPaAKgCm2(Unidades.KgCm2AMPa(350)), 1e-9);
            Igual("ida y vuelta MPa -> kg/cm2 -> MPa", 25, Unidades.KgCm2AMPa(Unidades.MPaAKgCm2(25)), 1e-9);

            ConfiguracionMateriales cfg = ConfiguracionMateriales.PorDefecto();
            Comprobar("8 resistencias", cfg.Catalogo.Count == 8, cfg.Catalogo.Count.ToString());
            for (int i = 0; i < Fcs.Length && i < cfg.Catalogo.Count; i++)
            {
                Igual("f'c " + F(Fcs[i]), Fcs[i], cfg.Catalogo[i].FcKgCm2);
                Igual("densidad normal " + F(Fcs[i]), 2400, cfg.Catalogo[i].DensidadKgM3);
            }
            Igual("prefijo por defecto", P, cfg.PrefijoNombre);
            Comprobar("actualizar existentes desactivado", !cfg.ActualizarExistentes);
            Comprobar("duplicar apariencia activado", cfg.DuplicarApariencia);
            Igual("clase de material", "Concreto", cfg.ClaseMaterial);
            Igual("palabras clave", "concreto, hormigón, Perú", cfg.PalabrasClave);
            Igual("material de apariencia por defecto", "Concrete, Cast-in-Place gray", cfg.MaterialApariencia[0]);
            Comprobar("tramas con alternativas en espanol e ingles", cfg.TramaCorte.Contains("Concrete") && cfg.TramaCorte.Contains("Hormigón") && cfg.TramaSuperficie.Count == 3);
            Comprobar("sin extras por defecto", cfg.CatalogoExtra.Count == 0);
            Igual("Poisson 0.15", 0.15, cfg.Reglas.Poisson);
            Igual("dilatacion 1e-5", 1e-5, cfg.Reglas.DilatacionTermicaPorC, 1e-12);

            // nombres: prefijo + f'c
            string[] esperados = { P + "140", P + "175", P + "210", P + "245", P + "280", P + "315", P + "350", P + "420" };
            for (int i = 0; i < esperados.Length; i++)
                Igual("nombre " + esperados[i], esperados[i], Nombres.GenerarConcreto(cfg.PrefijoNombre, cfg.Catalogo[i].FcKgCm2));
            Igual("nombre de catalogo 210", "210", Nombres.NombreCatalogoConcreto(210));
            Igual("nombre de catalogo 212.5 (punto decimal)", "212.5", Nombres.NombreCatalogoConcreto(212.5));
            Igual("nombre de catalogo 210.004 se redondea", "210", Nombres.NombreCatalogoConcreto(210.004));
            Igual("prefijo vacio", "280", Nombres.GenerarConcreto("", 280));
            Igual("prefijo nulo", "280", Nombres.GenerarConcreto(null, 280));
            Igual("prefijo personalizado", "C-280", Nombres.GenerarConcreto("C-", 280));
            Comprobar("NombreCatalogo de la entrada", cfg.Catalogo[2].NombreCatalogo == "210");
            Comprobar("comparacion sin mayusculas", Nombres.Iguales("concreto F'C 210", P + "210"));
        }

        private static void PruebasConcretoModulos()
        {
            ConfiguracionMateriales cfg = ConfiguracionMateriales.PorDefecto();
            var r = new ReglasConcreto(cfg.Reglas, cfg.Termico);

            // E = 15000 raiz(f'c) para 210: 217 371 kg/cm2 = 21 317 MPa
            Igual("E 210 kg/cm2", 217370.65, r.ModuloNormalKgCm2(210), 0.01);
            Igual("E 210 MPa", 21316.78, r.ModuloElasticidadMPa(210, 2400), 0.01);
            Igual("E 210 kg/cm2 con densidad normal", 217370.65, r.ModuloElasticidadKgCm2(210, 2400), 0.01);
            double[] eKg = { 177482.39, 198431.35, 217370.65, 234787.14, 250998.01, 266223.59, 280624.30, 307408.52 };
            double[] eMPa = { 17405.08, 19459.47, 21316.78, 23024.75, 24614.50, 26107.62, 27519.84, 30146.48 };
            for (int i = 0; i < Fcs.Length; i++)
            {
                ValoresConcreto v = r.Calcular(Fcs[i], 2400);
                Igual("E " + F(Fcs[i]) + " kg/cm2", eKg[i], v.EKgCm2, 0.01);
                Igual("E " + F(Fcs[i]) + " MPa", eMPa[i], v.EMPa, 0.01);
                Comprobar("E " + F(Fcs[i]) + " crece con f'c", i == 0 || v.EKgCm2 > eKg[i - 1]);
                Comprobar("formula normal " + F(Fcs[i]), v.DensidadNormal && v.FormulaE.Contains("15000"));
            }
            Igual("E 100", 150000, r.ModuloNormalKgCm2(100), 1e-6);
            Igual("E 1000", 474341.65, r.ModuloNormalKgCm2(1000), 0.01);
            Igual("15000 raiz(f'c) equivale a ~4700 raiz(f'c MPa)", 4697.3, r.ModuloElasticidadMPa(210, 2400) / Math.Sqrt(Unidades.KgCm2AMPa(210)), 0.1);

            // G = E / (2 (1 + nu)), nu = 0.15
            Igual("G 210 kg/cm2", 94508.98, ReglasConcreto.ModuloCorte(217370.65, 0.15), 0.01);
            ValoresConcreto v210 = r.Calcular(210, 2400);
            Igual("G 210 en Calcular", 94508.98, v210.GKgCm2, 0.01);
            Igual("G 210 MPa", 9268.17, v210.GMPa, 0.01);
            Igual("Poisson en Calcular", 0.15, v210.Poisson);
            Igual("G con nu = 0.20 (ACI)", 217370.65 / 2.4, ReglasConcreto.ModuloCorte(217370.65, 0.20), 1e-6);
            cfg.Reglas.Poisson = 0.20;
            Igual("G con Poisson editado en config", 217370.65 / 2.4, new ReglasConcreto(cfg.Reglas, cfg.Termico).Calcular(210, 2400).GKgCm2, 0.01);
            cfg.Reglas.Poisson = 0.15;

            // formula general E = wc^1.5 * 0.043 * raiz(f'c MPa)
            Igual("general 210 @ 2300 -> 21524 MPa", 21524.36, r.ModuloGeneralMPa(210, 2300), 0.01);
            Igual("general 210 @ 1800 -> 14902 MPa", 14902.09, r.ModuloGeneralMPa(210, 1800), 0.01);
            Igual("general 210 @ 1800 en kg/cm2", 151959.03, Unidades.MPaAKgCm2(r.ModuloGeneralMPa(210, 1800)), 0.01);
            double normal = r.ModuloElasticidadMPa(210, 2400);
            double general2300 = r.ModuloGeneralMPa(210, 2300);
            Comprobar("general @ 2300 esta un 1 % por encima de la normal", general2300 > normal && general2300 / normal < 1.015, F(general2300 / normal));
            Comprobar("general @ 1800 esta por debajo de la normal", r.ModuloGeneralMPa(210, 1800) < normal);

            // Calcular elige la formula por la densidad
            ValoresConcreto v2300 = r.Calcular(210, 2300);
            Comprobar("2300 no es la densidad normal (2400)", !v2300.DensidadNormal && !r.EsDensidadNormal(2300));
            Igual("2300 usa la formula general", general2300, v2300.EMPa, 1e-9);
            Comprobar("texto de la formula general", v2300.FormulaE.Contains("wc^1.5") && v2300.FormulaE.Contains("2300"), v2300.FormulaE);
            ValoresConcreto v1800 = r.Calcular(210, 1800);
            Igual("1800 usa la formula general", 14902.09, v1800.EMPa, 0.01);
            Igual("G con 1800", 14902.09 / 2.3, v1800.GMPa, 0.01);
            Comprobar("2400 usa la formula normal aunque la general daria 22943", v210.DensidadNormal && Math.Abs(v210.EMPa - 21316.78) < 0.01 && Math.Abs(r.ModuloGeneralMPa(210, 2400) - 22943.27) < 0.01);

            // densidad normal editable en config: con 2300 como normal, 2300 usa la simplificada
            cfg.Reglas.DensidadNormalKgM3 = 2300;
            var r2 = new ReglasConcreto(cfg.Reglas, cfg.Termico);
            Comprobar("densidad normal 2300 en config", r2.EsDensidadNormal(2300) && !r2.EsDensidadNormal(2400));
            Igual("con normal = 2300, 2300 usa 15000 raiz(f'c)", 21316.78, r2.ModuloElasticidadMPa(210, 2300), 0.01);
            Igual("con normal = 2300, 2400 usa la general", 22943.27, r2.ModuloElasticidadMPa(210, 2400), 0.01);

            Igual("f'c 0 -> E 0", 0, r.ModuloNormalKgCm2(0));
            Igual("f'c negativo -> E 0", 0, r.ModuloGeneralMPa(-5, 2300));
        }

        private static void PruebasConcretoLigero()
        {
            ConfiguracionMateriales cfg = ConfiguracionMateriales.PorDefecto();
            var r = new ReglasConcreto(cfg.Reglas, cfg.Termico);

            Comprobar("1800 es ligero", r.EsLigero(1800));
            Comprobar("1899 es ligero", r.EsLigero(1899));
            Comprobar("1900 no es ligero (limite)", !r.EsLigero(1900));
            Comprobar("2400 no es ligero", !r.EsLigero(2400));
            Igual("factor de corte normal 1.0", 1.0, r.FactorCorte(2400));
            Igual("factor de corte ligero 0.75", 0.75, r.FactorCorte(1800));
            ValoresConcreto vl = r.Calcular(210, 1800), vn = r.Calcular(210, 2400);
            Comprobar("Calcular: ligero", vl.Ligero && !vn.Ligero);
            Igual("Calcular: factor ligero", 0.75, vl.FactorCorte);
            Igual("Calcular: factor normal", 1.0, vn.FactorCorte);

            // tabla termica: dos juegos, la conductividad cambia y el resto es igual
            Igual("conductividad normal 1.046", 1.046, vn.Termico.ConductividadWmK);
            Igual("conductividad ligero 0.5", 0.5, vl.Termico.ConductividadWmK);
            Igual("calor especifico 0.657", 0.657, vn.Termico.CalorEspecificoJgC);
            Igual("calor especifico ligero igual", 0.657, vl.Termico.CalorEspecificoJgC);
            Igual("emisividad 0.95", 0.95, vn.Termico.Emisividad);
            Igual("permeabilidad 182.4", 182.4, vn.Termico.PermeabilidadNgPaSm2);
            Igual("porosidad 0.01", 0.01, vn.Termico.Porosidad);
            Igual("reflectividad 0", 0, vn.Termico.Reflectividad);
            Igual("resistividad 2e9", 2.0e9, vn.Termico.ResistividadOhmM);
            Comprobar("no transmite luz", !vn.Termico.TransmiteLuz && !vl.Termico.TransmiteLuz);
            Comprobar("la densidad del activo termico es la de la fila", vl.DensidadKgM3 == 1800 && vn.DensidadKgM3 == 2400);
            Comprobar("umbral ligero editable", !new ReglasConcreto(new ReglasConcretoCfg { DensidadLigeroKgM3 = 1700 }, cfg.Termico).EsLigero(1800));
            cfg.Termico.Ligero.ConductividadWmK = 0.6;
            Igual("conductividad ligero editada en config", 0.6, new ReglasConcreto(cfg.Reglas, cfg.Termico).Calcular(210, 1800).Termico.ConductividadWmK);

            // gris: mas oscuro cuanto mayor f'c
            Comprobar("gris 100 -> 210", ReglasConcreto.Gris(100) == 210, ReglasConcreto.Gris(100).ToString());
            Comprobar("gris 140 -> 207", ReglasConcreto.Gris(140) == 207, ReglasConcreto.Gris(140).ToString());
            Comprobar("gris 210 -> 201", ReglasConcreto.Gris(210) == 201, ReglasConcreto.Gris(210).ToString());
            Comprobar("gris 420 -> 184", ReglasConcreto.Gris(420) == 184, ReglasConcreto.Gris(420).ToString());
            Comprobar("gris 1000 -> 138", ReglasConcreto.Gris(1000) == 138, ReglasConcreto.Gris(1000).ToString());
            Comprobar("gris acotado por debajo", ReglasConcreto.Gris(5000) == 110 && ReglasConcreto.Gris(-100) == 220);
            for (int i = 1; i < Fcs.Length; i++)
                Comprobar("gris decrece " + F(Fcs[i]), ReglasConcreto.Gris(Fcs[i]) < ReglasConcreto.Gris(Fcs[i - 1]));
            Comprobar("gris en Calcular", vn.Gris == 201);

            // descripcion con coma decimal
            Igual("descripcion 210", "f'c = 210 kg/cm² (20,6 MPa), E.060", ReglasConcreto.Descripcion(210));
            Igual("descripcion 280", "f'c = 280 kg/cm² (27,5 MPa), E.060", ReglasConcreto.Descripcion(280));
            Igual("descripcion 212.5", "f'c = 212,5 kg/cm² (20,8 MPa), E.060", ReglasConcreto.Descripcion(212.5));
            Igual("descripcion en Calcular", ReglasConcreto.Descripcion(210), vn.Descripcion);
        }

        private static void PruebasConcretoRango()
        {
            ConfiguracionMateriales cfg = ConfiguracionMateriales.PorDefecto();
            var r = new ReglasConcreto(cfg.Reglas, cfg.Termico);
            Igual("minimo f'c", 100, cfg.Reglas.FcMinimoKgCm2);
            Igual("maximo f'c", 1000, cfg.Reglas.FcMaximoKgCm2);
            Comprobar("100 dentro (limite)", r.DentroDeNorma(100, 2400));
            Comprobar("1000 dentro (limite)", r.DentroDeNorma(1000, 2400));
            Comprobar("210 dentro", r.DentroDeNorma(210, 2400) && r.MotivoFueraDeNorma(210, 2400) == null);
            Comprobar("99.99 fuera", !r.DentroDeNorma(99.99, 2400));
            Comprobar("1000.01 fuera", !r.DentroDeNorma(1000.01, 2400));
            Comprobar("50 fuera", !r.DentroDeNorma(50, 2400));
            Comprobar("1200 fuera", !r.DentroDeNorma(1200, 2400));
            Igual("motivo 50", "f'c menor de 100 kg/cm2, fuera de rango", r.MotivoFueraDeNorma(50, 2400));
            Igual("motivo 1200", "f'c mayor de 1000 kg/cm2, fuera de rango", r.MotivoFueraDeNorma(1200, 2400));
            Igual("motivo 0", "f'c no valido", r.MotivoFueraDeNorma(0, 2400));
            Igual("motivo negativo", "f'c no valido", r.MotivoFueraDeNorma(-210, 2400));

            // densidad: 1450 a 2500 (formula general E.060)
            Comprobar("1450 dentro (limite)", r.DentroDeNorma(210, 1450));
            Comprobar("2500 dentro (limite)", r.DentroDeNorma(210, 2500));
            Comprobar("1449 fuera", !r.DentroDeNorma(210, 1449));
            Comprobar("2501 fuera", !r.DentroDeNorma(210, 2501));
            Igual("motivo densidad 1000", "densidad menor de 1450 kg/m3, fuera de la formula E.060", r.MotivoFueraDeNorma(210, 1000));
            Igual("motivo densidad 3000", "densidad mayor de 2500 kg/m3, fuera de la formula E.060", r.MotivoFueraDeNorma(210, 3000));
            Igual("motivo densidad 0", "densidad no valida", r.MotivoFueraDeNorma(210, 0));
            Comprobar("f'c fuera manda sobre densidad fuera", (r.MotivoFueraDeNorma(50, 1000) ?? "").StartsWith("f'c"));
            Comprobar("Calcular.DentroDeNorma", r.Calcular(210, 2400).DentroDeNorma && !r.Calcular(50, 2400).DentroDeNorma && !r.Calcular(210, 1000).DentroDeNorma);
            Comprobar("fuera de rango se sigue calculando (para mostrar)", r.Calcular(50, 2400).EKgCm2 > 0);

            // rango editable en config
            var c2 = ConfiguracionMateriales.Deserializar("{ \"reglas\": { \"fcMinimoKgCm2\": 175, \"fcMaximoKgCm2\": 500 } }");
            var r2 = new ReglasConcreto(c2.Reglas, c2.Termico);
            Comprobar("rango de config: 140 fuera, 175 dentro, 500 dentro, 501 fuera",
                      !r2.DentroDeNorma(140, 2400) && r2.DentroDeNorma(175, 2400) && r2.DentroDeNorma(500, 2400) && !r2.DentroDeNorma(501, 2400));
            var mal = ConfiguracionMateriales.Deserializar("{ \"reglas\": { \"fcMinimoKgCm2\": 0, \"fcMaximoKgCm2\": -1, \"densidadMinimaKgM3\": 0, \"densidadMaximaKgM3\": 0 } }");
            Comprobar("rango invalido vuelve al de por defecto", mal.Reglas.FcMinimoKgCm2 == 100 && mal.Reglas.FcMaximoKgCm2 == 1000 &&
                      mal.Reglas.DensidadMinimaKgM3 == 1450 && mal.Reglas.DensidadMaximaKgM3 == 2500);
        }

        private static void PruebasConcretoPlanificador()
        {
            ConfiguracionMateriales cfg = ConfiguracionMateriales.PorDefecto();
            var existentes = new List<MaterialExistente>
            {
                new MaterialExistente(P + "210", "Concreto"),      // igual
                new MaterialExistente("concreto F'C 280", "Madera"), // solo cambia la mayuscula, otra clase
                new MaterialExistente("Concrete, Cast-in-Place gray", "Concrete") // ajeno al catalogo
            };

            // a) sin actualizar: idempotente
            List<FilaPlanConcreto> plan = PlanificadorConcreto.Planificar(cfg, P, existentes, false);
            Comprobar("8 filas", plan.Count == 8);
            FilaPlanConcreto f210 = plan.First(f => f.Concreto.FcKgCm2 == 210);
            FilaPlanConcreto f280 = plan.First(f => f.Concreto.FcKgCm2 == 280);
            FilaPlanConcreto f140 = plan.First(f => f.Concreto.FcKgCm2 == 140);
            Comprobar("210 ya existe", f210.Estado == EstadoFila.YaExiste && !f210.Crear && f210.Aviso == null);
            Comprobar("280 ya existe (sin distinguir mayusculas) con aviso de clase", f280.Estado == EstadoFila.YaExiste && f280.Aviso == "clase Madera", f280.Aviso);
            Igual("texto de estado 280", "ya existe (clase Madera)", f280.TextoEstado);
            Comprobar("140 se creara", f140.Estado == EstadoFila.SeCreara && f140.Crear && f140.Nombre == P + "140");
            Comprobar("6 filas a crear", plan.Count(f => f.Crear) == 6);
            Comprobar("existentes no seleccionables", !f210.Seleccionable && f140.Seleccionable);
            Comprobar("valores en la fila", Math.Abs(f210.Valores.EKgCm2 - 217370.65) < 0.01 && f210.Valores.Gris == 201);
            Comprobar("ninguna es extra", plan.All(f => !f.EsExtra));

            // b) actualizar existentes
            plan = PlanificadorConcreto.Planificar(cfg, P, existentes, true);
            Comprobar("actualizar: 210 se actualizara", plan.First(f => f.Concreto.FcKgCm2 == 210).Estado == EstadoFila.SeActualizara);
            Comprobar("actualizar: 8 marcadas", plan.Count(f => f.Crear) == 8);
            Comprobar("actualizar: aviso de clase se conserva", plan.First(f => f.Concreto.FcKgCm2 == 280).Aviso == "clase Madera");

            // c) otro prefijo: nada existe
            plan = PlanificadorConcreto.Planificar(cfg, "C-", existentes, false);
            Comprobar("prefijo C-: todo se creara", plan.All(f => f.Estado == EstadoFila.SeCreara) && plan[0].Nombre == "C-140");

            // d) validador de Revit (simulado: rechaza el apostrofo)
            plan = PlanificadorConcreto.Planificar(cfg, P, existentes, false, n => !n.Contains("'"));
            Comprobar("nombres con apostrofo no validos", plan.Count(f => f.Estado == EstadoFila.NombreInvalido) == 8);
            Comprobar("nombre no valido no se marca", plan.All(f => !f.Crear && !f.Seleccionable));
            plan = PlanificadorConcreto.Planificar(cfg, "Concreto fc ", existentes, false, n => !n.Contains("'"));
            Comprobar("con otro prefijo todos validos", plan.All(f => f.Estado != EstadoFila.NombreInvalido));

            // e) sin materiales existentes
            plan = PlanificadorConcreto.Planificar(cfg, P, null, false);
            Comprobar("sin existentes: 8 a crear", plan.Count(f => f.Crear) == 8);

            // f) extras: lectura, limpieza y planificacion
            string json = "{ \"catalogoExtra\": [" +
                          " { \"fcKgCm2\": 300, \"densidadKgM3\": 1800 }," +
                          " { \"fcKgCm2\": 210, \"densidadKgM3\": 2400 }," +
                          " { \"fcKgCm2\": 50 }," +
                          " { \"fcKgCm2\": 1200 }," +
                          " { \"fcKgCm2\": 250, \"densidadKgM3\": 1000 }," +
                          " { \"fcKgCm2\": 300.004, \"densidadKgM3\": 2400 }," +
                          " { \"fcKgCm2\": 0 }," +
                          " { \"densidadKgM3\": 2400 } ] }";
            ConfiguracionMateriales c = ConfiguracionMateriales.Deserializar(json);
            Comprobar("catalogo original intacto (8)", c.Catalogo.Count == 8);
            Comprobar("extras: 4 validos (se quitan el 210 repetido, el 300 repetido, el 0 y el sin f'c)", c.CatalogoExtra.Count == 4, c.CatalogoExtra.Count.ToString());
            Comprobar("extras: 210 no se duplica", !c.CatalogoExtra.Any(x => x.FcKgCm2 == 210));
            Comprobar("extras: 300.004 (mismo nombre) se descarta", c.CatalogoExtra.Count(x => x.NombreCatalogo == "300") == 1 && c.CatalogoExtra.First(x => x.NombreCatalogo == "300").DensidadKgM3 == 1800);
            Comprobar("extras sin densidad toman la normal", c.CatalogoExtra.First(x => x.FcKgCm2 == 50).DensidadKgM3 == 2400);
            Comprobar("ida y vuelta conserva los extras", ConfiguracionMateriales.Deserializar(c.Serializar()).CatalogoExtra.Count == 4);

            plan = PlanificadorConcreto.Planificar(c, P, existentes, false);
            Comprobar("12 filas", plan.Count == 12, plan.Count.ToString());
            Comprobar("las 8 primeras no son extra y las 4 ultimas si", plan.Take(8).All(f => !f.EsExtra) && plan.Skip(8).All(f => f.EsExtra));
            FilaPlanConcreto f300 = plan.First(f => f.Concreto.FcKgCm2 == 300);
            FilaPlanConcreto f50 = plan.First(f => f.Concreto.FcKgCm2 == 50);
            FilaPlanConcreto f1200 = plan.First(f => f.Concreto.FcKgCm2 == 1200);
            FilaPlanConcreto f250 = plan.First(f => f.Concreto.FcKgCm2 == 250);
            Comprobar("300 @ 1800 se creara, ligero, con la formula general", f300.Estado == EstadoFila.SeCreara && f300.Crear && f300.Nombre == P + "300" && f300.Valores.Ligero && !f300.Valores.DensidadNormal);
            Igual("300 @ 1800: E MPa", Math.Pow(1800, 1.5) * 0.043 * Math.Sqrt(Unidades.KgCm2AMPa(300)), f300.Valores.EMPa, 1e-6);
            Comprobar("50 fuera de rango: no se crea", f50.Estado == EstadoFila.FueraDeNorma && !f50.Crear && !f50.Seleccionable);
            Igual("50 texto de estado", "no se crea (f'c menor de 100 kg/cm2, fuera de rango)", f50.TextoEstado);
            Igual("1200 texto de estado", "no se crea (f'c mayor de 1000 kg/cm2, fuera de rango)", f1200.TextoEstado);
            Igual("250 @ 1000 texto de estado", "no se crea (densidad menor de 1450 kg/m3, fuera de la formula E.060)", f250.TextoEstado);
            Comprobar("fuera de rango tampoco con actualizar", PlanificadorConcreto.Planificar(c, P, existentes, true).Count(f => f.Estado == EstadoFila.FueraDeNorma) == 3);
            Comprobar("7 filas a crear (8 - 2 existentes + 1 extra valido)", plan.Count(f => f.Crear) == 7, plan.Count(f => f.Crear).ToString());

            // g) nombre repetido en la tabla (dos entradas del catalogo con el mismo nombre)
            var c2 = ConfiguracionMateriales.Deserializar("{ \"catalogo\": [ { \"fcKgCm2\": 210 }, { \"fcKgCm2\": 210.004 }, { \"fcKgCm2\": 280 } ] }");
            List<FilaPlanConcreto> plan2 = PlanificadorConcreto.Planificar(c2, P, null, false);
            Comprobar("dos filas producen " + P + "210: ambas repetidas", plan2.Count(f => f.Estado == EstadoFila.NombreDuplicado) == 2 && plan2.Where(f => f.Estado == EstadoFila.NombreDuplicado).All(f => !f.Crear));
            Igual("texto repetido", "nombre repetido en la tabla", plan2.First(f => f.Estado == EstadoFila.NombreDuplicado).TextoEstado);
            Comprobar("la de 280 se crea", plan2.First(f => f.Concreto.FcKgCm2 == 280).Crear);

            // h) validacion de una resistencia nueva antes de anadirla
            ConfiguracionMateriales c3 = ConfiguracionMateriales.PorDefecto();
            c3.CatalogoExtra.Add(new ConcretoCatalogo(300, 1800));
            var proyecto = new List<MaterialExistente> { new MaterialExistente(P + "500", "Concreto"), new MaterialExistente("Acero", "Metal") };
            Func<string, bool> valido = n => !n.Contains("|");
            Comprobar("valida: 250 @ 2400", PlanificadorConcreto.ValidarNuevoConcreto(c3, P, 250, 2400, proyecto, valido) == null);
            Comprobar("valida: 350.5 @ 1600", PlanificadorConcreto.ValidarNuevoConcreto(c3, P, 350.5, 1600, proyecto, valido) == null);
            Comprobar("valida: limites 100 @ 1450 y 1000 @ 2500", PlanificadorConcreto.ValidarNuevoConcreto(c3, P, 100, 1450, proyecto, valido) == null && PlanificadorConcreto.ValidarNuevoConcreto(c3, P, 1000, 2500, proyecto, valido) == null);
            Igual("rechaza: 50", "f'c menor de 100 kg/cm2, fuera de rango", PlanificadorConcreto.ValidarNuevoConcreto(c3, P, 50, 2400, proyecto, valido));
            Igual("rechaza: 1200", "f'c mayor de 1000 kg/cm2, fuera de rango", PlanificadorConcreto.ValidarNuevoConcreto(c3, P, 1200, 2400, proyecto, valido));
            Igual("rechaza: densidad 1000", "densidad menor de 1450 kg/m3, fuera de la formula E.060", PlanificadorConcreto.ValidarNuevoConcreto(c3, P, 250, 1000, proyecto, valido));
            Igual("rechaza: repetido en catalogo", "f'c 210 ya esta en el catalogo", PlanificadorConcreto.ValidarNuevoConcreto(c3, P, 210, 2400, proyecto, valido));
            Igual("rechaza: repetido en catalogo con otra densidad", "f'c 210 ya esta en el catalogo", PlanificadorConcreto.ValidarNuevoConcreto(c3, P, 210, 1800, proyecto, valido));
            Igual("rechaza: repetido en extras", "f'c 300 ya esta en la lista", PlanificadorConcreto.ValidarNuevoConcreto(c3, P, 300, 2400, proyecto, valido));
            Igual("rechaza: choca con material del proyecto", "el proyecto ya tiene un material \"" + P + "500\"", PlanificadorConcreto.ValidarNuevoConcreto(c3, P, 500, 2400, proyecto, valido));
            Comprobar("con otro prefijo 500 ya no choca", PlanificadorConcreto.ValidarNuevoConcreto(c3, "C-", 500, 2400, proyecto, valido) == null);
            Comprobar("rechaza: nombre no admitido por Revit", (PlanificadorConcreto.ValidarNuevoConcreto(c3, "a|b ", 250, 2400, proyecto, valido) ?? "").StartsWith("Revit no admite"));
            Comprobar("sin existentes ni validador", PlanificadorConcreto.ValidarNuevoConcreto(c3, P, 250, 2400, null) == null);
        }

        private static void PruebasConcretoConfig()
        {
            // a) el materiales.json distribuido equivale a la configuracion por defecto
            string ruta = Path.Combine(AppContext.BaseDirectory, "materiales.json");
            Comprobar("materiales.json copiado junto a las pruebas", File.Exists(ruta), ruta);
            ConfiguracionMateriales leida = ConfiguracionMateriales.Cargar(ruta);
            Comprobar("materiales.json: 8 resistencias", leida.Catalogo.Count == 8);
            Igual("materiales.json equivale al por defecto", ConfiguracionMateriales.PorDefecto().Serializar(), leida.Serializar());

            // b) comentarios, comas finales, camelCase y valores parciales
            string json = "{\n  // comentario\n  \"prefijoNombre\": \"C-\",\n  \"actualizarExistentes\": true,\n  \"duplicarApariencia\": false,\n" +
                          "  \"tramaCorte\": [ \"Hormigón\", ],\n" +
                          "  \"catalogo\": [ { \"fcKgCm2\": 175, }, { \"fcKgCm2\": 210, \"densidadKgM3\": 2300 }, ],\n" +
                          "  \"reglas\": { \"poisson\": 0.2, \"densidadLigeroKgM3\": 1850, },\n" +
                          "  \"termico\": { \"ligero\": { \"conductividadWmK\": 0.6, }, },\n}\n";
            ConfiguracionMateriales c = ConfiguracionMateriales.Deserializar(json);
            Igual("prefijo leido", "C-", c.PrefijoNombre);
            Comprobar("actualizarExistentes leido", c.ActualizarExistentes);
            Comprobar("duplicarApariencia leido", !c.DuplicarApariencia);
            Comprobar("tramaCorte de 1 entrada, tramaSuperficie por defecto", c.TramaCorte.Count == 1 && c.TramaSuperficie.Count == 3);
            Comprobar("catalogo de 2 entradas", c.Catalogo.Count == 2);
            Comprobar("densidad omitida toma la normal", c.Catalogo[0].DensidadKgM3 == 2400 && c.Catalogo[1].DensidadKgM3 == 2300);
            Igual("poisson leido", 0.2, c.Reglas.Poisson);
            Igual("umbral ligero leido", 1850, c.Reglas.DensidadLigeroKgM3);
            Igual("dilatacion no indicada: por defecto", 1e-5, c.Reglas.DilatacionTermicaPorC, 1e-12);
            Igual("termico ligero conductividad leida", 0.6, c.Termico.Ligero.ConductividadWmK);
            Igual("termico ligero: el resto por defecto", 0.657, c.Termico.Ligero.CalorEspecificoJgC);
            Igual("termico normal no indicado: por defecto", 1.046, c.Termico.Normal.ConductividadWmK);

            // c) valores sin sentido vuelven al por defecto
            var mal = ConfiguracionMateriales.Deserializar("{ \"reglas\": { \"poisson\": 0.9, \"dilatacionTermicaPorC\": -1, \"factorCorteLigero\": 0 }," +
                                                           " \"termico\": { \"normal\": { \"conductividadWmK\": -1, \"emisividad\": 2 }, \"ligero\": null }," +
                                                           " \"materialApariencia\": [], \"prefijoNombre\": null }");
            Comprobar("poisson 0.9 -> 0.15", mal.Reglas.Poisson == 0.15);
            Comprobar("dilatacion negativa -> 1e-5", Math.Abs(mal.Reglas.DilatacionTermicaPorC - 1e-5) < 1e-12);
            Comprobar("factor de corte 0 -> 0.75", mal.Reglas.FactorCorteLigero == 0.75);
            Comprobar("conductividad negativa -> 1.046", mal.Termico.Normal.ConductividadWmK == 1.046);
            Comprobar("emisividad 2 -> 0.95", mal.Termico.Normal.Emisividad == 0.95);
            Comprobar("termico ligero nulo -> por defecto", mal.Termico.Ligero != null && mal.Termico.Ligero.ConductividadWmK == 0.5);
            Comprobar("lista de apariencia vacia -> por defecto", mal.MaterialApariencia.Count == 6);
            Igual("prefijo nulo -> vacio", "", mal.PrefijoNombre);

            // d) archivo inexistente, JSON vacio y JSON invalido
            Comprobar("sin archivo: por defecto", ConfiguracionMateriales.Cargar(Path.Combine(AppContext.BaseDirectory, "no_existe.json")).Catalogo.Count == 8);
            ConfiguracionMateriales vacio = ConfiguracionMateriales.Deserializar("{}");
            Comprobar("JSON vacio: catalogo por defecto", vacio.Catalogo.Count == 8);
            Igual("JSON vacio: prefijo por defecto", P, vacio.PrefijoNombre);
            bool lanza = false;
            try { ConfiguracionMateriales.Deserializar("{ esto no es json"); } catch { lanza = true; }
            Comprobar("JSON invalido lanza excepcion", lanza);

            // e) ida y vuelta con extras y clon
            leida.CatalogoExtra.Add(new ConcretoCatalogo(300, 1800));
            ConfiguracionMateriales clon = leida.Clonar();
            Igual("clon identico", leida.Serializar(), clon.Serializar());
            Comprobar("clon conserva el extra", clon.CatalogoExtra.Count == 1 && clon.CatalogoExtra[0].DensidadKgM3 == 1800);
            Comprobar("serializado legible (acentos sin escapar)", leida.Serializar().Contains("\"palabrasClave\": \"concreto, hormigón, Perú\""));
            Comprobar("nombreCatalogo no se serializa", !leida.Serializar().Contains("nombreCatalogo"));
        }
    }
}
