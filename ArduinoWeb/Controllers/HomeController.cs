using ArduinoWeb.Data;
using ArduinoWeb.Models;
using ArduinoWeb.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ArduinoWeb.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ArduinoDbContext _context;

        public HomeController(ILogger<HomeController> logger, ArduinoDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        /// <summary>
        /// Home page - Lista dispositivos, Localizacoes
        /// �ltimas 10 leituras
        /// leituras para um dado dispositivo
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public IActionResult Index(int? id = null)
        {
            var vm = new DispositivoVm { RelatorioDispositivoId = id };
            RelatorioDispositivo dispositivo = null;

            // define a lista de dispositivos e Localizacoes para mostrar
            if (_context.RelatorioDispositivos.Any())
            {
                vm.RelatorioDispositivos = _context.RelatorioDispositivos.ToList();

                // devolve o primeiro dispositivo 
                dispositivo = vm.RelatorioDispositivos.First();
            }

            if (_context.Localizacoes.Any())
            {
                vm.Localizacoes = _context.Localizacoes.ToList();
            }

            // carregar dispositivo
            if (id.HasValue)
            {
                dispositivo = _context.RelatorioDispositivos.FirstOrDefault(d => d.RelatorioDispositivoId == id.Value);
            }

            if (dispositivo != null)
            {
                vm.RelatorioDispositivoId = dispositivo.RelatorioDispositivoId;
                vm.TipoNome = dispositivo.Nome;
                vm.LocalizacaoNome = dispositivo.Localizacao.Nome;
                vm.LocalIp = dispositivo.UltimoIpAddress;
                vm.LastSet = MaisRecentes(dispositivo.RelatorioDispositivoId);
            }

            return View(vm);
        }

        /// <summary>
        /// devolve leituras mais recentes de um dispositivo espec�fico
        /// </summary>
        /// <param name="relatorioDispositivoId"></param>
        /// <returns></returns>
        public MedicaoVm MaisRecentes(int relatorioDispositivoId)
        {
            var recente = new MedicaoVm();

            var last3 = _context.Medicoes
                .Where(m => m.RelatorioDispositivoId == relatorioDispositivoId)
                .Select(m => m).Include(l => l.Localizacao).Distinct().
                OrderByDescending(m => m.DataMedicao).Take(3).ToList();

            if (last3.Any())
            {
                var humidade = last3.FirstOrDefault(m => m.TipoMedicaoId == 1);


                if (humidade != null)
                {
                    recente.DataMedicao = humidade.DataMedicao;
                    recente.Humidade = humidade.ValorLido;
                }

              
            }

            return recente;
        }

        /// <summary>
        /// devolve leituras
        /// </summary>
        /// <returns></returns>
        public IActionResult MaisRecentesPorLocal(int? localizacaoId = null)
        {
            var recente = new MedicaoVm();

            var last3 = _context.Medicoes
                .Where(m => m.LocalizacaoId == localizacaoId)
                .Select(m => m).Include(l => l.Localizacao).Distinct().
                OrderByDescending(m => m.DataMedicao).Take(3).ToList();

            if (last3.Any())
            {
                var temp = last3.FirstOrDefault(m => m.TipoMedicaoId == 1);
                var humd = last3.FirstOrDefault(m => m.TipoMedicaoId == 2);
                var lght = last3.FirstOrDefault(m => m.TipoMedicaoId == 3);

                if (temp != null)
                {
                    recente.DataMedicao = temp.DataMedicao;
                    recente.Humidade = temp.ValorLido;
                }

                if (humd != null) { recente.Humidade = humd.ValorLido; }
  
            }

            return View(recente);
        }

        /// <summary>
        /// devolve leituras
        /// </summary>
        /// <returns></returns>
        public IActionResult TodasPorLocal(int localizacaoId)
        {
            List<Medicao> recente = new List<Medicao>();

            recente = _context.Medicoes
                .Where(m => m.LocalizacaoId == localizacaoId)
                .Select(m => m).Include(l => l.Localizacao).
                Include(t => t.TipoMedicao).
                OrderByDescending(m => m.DataMedicao).ToList();

           

            return View(recente);
        }


        /// <summary>
        /// Show view allowing add of a location
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public IActionResult AddLocation()
        {
            return View(new LocalizacaoHandlerVm());
        }


        /// <summary>
        /// Do the work of adding a location
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPost]
        public IActionResult AddLocation(LocalizacaoHandlerVm model)
        {
            if (@ModelState.IsValid)
            {
                // check if name is in use
                if (_context.Localizacoes.Any(l => l.Nome.Equals(model.NomeLocalizacao)))
                {
                    model.Sucesso = false;
                    model.Mensagem = "Name is in use";
                }
                else
                {
                    // didn't use an identity seed for location so I have to manually increment
                    var addedId = _context.Localizacoes.Max(l => l.LocalizacaoId) + 1;
                    var addedLoc = new Localizacao
                    { LocalizacaoId = addedId, Nome = model.NomeLocalizacao, Descricao = model.LocalizacaoDescricao };

                    _context.Localizacoes.Add(addedLoc);
                    _context.SaveChanges();

                    // will not assume user wants to move a device to this location yet so just head back to home page
                    return RedirectToAction("Index", "Home");
                }
            }

            return View(model);
        }


        /// <summary>
        /// �oce um dispositivo para uma nova localiza��o
        /// </summary>
        /// <param name="relatorioDispositivoId"></param>
        /// <param name="localizacaoId"></param>
        /// <returns></returns>
        public ActionResult AlteraLocalizacao(int relatorioDispositivoId, int localizacaoId)
        {
            var model = new LocalizacaoHandlerVm { RelatorioDispositivoId = relatorioDispositivoId, Sucesso = false };
            var device = _context.RelatorioDispositivos.Include(t => t.Dispositivo).FirstOrDefault(d => d.RelatorioDispositivoId == relatorioDispositivoId);
            var location = _context.Localizacoes.FirstOrDefault(l => l.LocalizacaoId == localizacaoId);

            if (device == null) { model.Mensagem = $"Device with ID {relatorioDispositivoId} not found"; }
            else if (location == null) { model.Mensagem = $"Location with ID {localizacaoId} not found"; }
            else
            {
                device.LocalizacaoId = location.LocalizacaoId;
                _context.RelatorioDispositivos.Attach(device);
                _context.Entry(device).State = EntityState.Modified;
                _context.SaveChanges();

                model.DispositivoNome = device.Dispositivo.Nome;
                model.NomeLocalizacao = location.Nome;
                model.Sucesso = true;
            }

            return View(model);
        }


        /// <summary>
        /// Method that receives the three values from the device and posts them to the database
        /// </summary>
        /// <param name="id"></param>
        /// <param name="ip"></param>
        /// <param name="humidade"></param>
        /// <returns></returns>
        public ActionResult PostData(int id, string ip, decimal? hum)
        {
            var results = "Sucesso";
            var reported = DateTime.Now;

         

            try
            {
                var device = _context.RelatorioDispositivos.FirstOrDefault(d => d.RelatorioDispositivoId == id);

                if (device == null)
                {
                    results = "Dispositivo desconhecido";
                }
                else
                {
                    // atualizar o ip address primeiro
                    device.UltimoIpAddress = ip;

                    _context.RelatorioDispositivos.Attach(device);
 

                    if (hum.HasValue)
                    {
                        // add temperature
                        _context.Medicoes.Add(new Medicao
                        {
                            TipoMedicaoId = (int)TipoMedicaoEnum.Humidade,
                            RelatorioDispositivoId = device.RelatorioDispositivoId,
                            LocalizacaoId = device.LocalizacaoId,
                            ValorLido = hum.Value,
                            DataMedicao = reported
                        });
                    }


                    // gravar
                    _context.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                results = "Erro: " + ex.Message;
            }

            return Content(results);
        }

        /// <summary>
        /// Devolve dados por dia/24 horas para um determinado dispositivo
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public IActionResult DeviceDay(int? id)
        {
            // establish an empty table
            var gdataTable = new GoogleVizDataTable();

            gdataTable.cols.Add(new GoogleVizDataTable.Col { label = "Hora do Dia", type = "datetime" });

            gdataTable.cols.Add(new GoogleVizDataTable.Col { label = "Humidade %", type = "number" });


            // if ID given is present
            if (id.HasValue)
            {
                // next get the most recent measurement for this device
                var mostRecent = _context.Medicoes.Where(d => d.RelatorioDispositivoId == id.Value)
                    .Select(m => m).OrderByDescending(m => m.DataMedicao).Take(1).FirstOrDefault();

                // if we have a recent measurement for this device
                if (mostRecent != null)
                {
                    // establish a range of previous to current day/time
                    var finish = mostRecent.DataMedicao;
                    var start = finish.AddDays(-1);

                    // fetch a set of measurements for that range
                    var recentSet = MedicaoSetRange(id.Value, start, finish);

                    // contr�i a datatable do google usando os dados anteriores atrav�s o m�todo (MedicaoSetRange(id.Value, start, finish))
                    gdataTable.rows =
                        (from set in recentSet
                         select new GoogleVizDataTable.Row
                         {
                             c = new List<GoogleVizDataTable.Row.RowValue>
                            {
                                new GoogleVizDataTable.Row.RowValue { v = set.GoogleDate },
                                new GoogleVizDataTable.Row.RowValue { v = set.Humidade },

                            }
                         }).ToList();
                }

            }

            return Json(gdataTable);
        }

        /// <summary>
        /// Build an aggregate list last day's worth of measurements, i.e.
        /// from the most recent measurement back to 24 hours previous, but
        /// averaged by hour
        /// </summary>
        /// <param name="relatorioDispositivoId">Specific device ID for which to fetch a set of measurments</param>
        /// <param name="start">Start date/time for which to fetch set of measurements</param>
        /// <param name="finish">Finishing date/time for which to fetch set of measurements</param>
        /// <returns></returns>
        public List<MedicaoVm> MedicaoSetRange(int relatorioDispositivoId, DateTime start, DateTime finish)
        {
            // constr�i o conjunto de medi��es
            var measureSet =
                (from m in _context.Medicoes.Select(m => m).Include(l => l.Localizacao).AsEnumerable()
                 where m.RelatorioDispositivoId == relatorioDispositivoId
                 && m.DataMedicao >= start
                 && m.DataMedicao <= finish
                 orderby m.DataMedicao
                 group m by new { MeasuredDate = DateTime.Parse(m.DataMedicao.ToString("yyyy-MM-dd HH:mm:ss")), m.Localizacao.Nome }
                    into g
                 select new MedicaoVm
                 {
                     DataMedicao = g.Key.MeasuredDate,
                     //NomeLocalizacao = g.Key.Nome,
                     Humidade = g.Where(m => m.TipoMedicaoId == 1).Select(r => r.ValorLido).FirstOrDefault(),

                 }).ToList();

            return measureSet;
        }

        public IActionResult Todas()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
