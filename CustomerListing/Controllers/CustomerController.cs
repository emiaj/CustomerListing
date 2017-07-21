using System;
using System.Threading.Tasks;
using System.Web.Mvc;
using Akka.Actor;

namespace CustomerListing.Controllers
{
    public class CustomerController : Controller
    {
        private readonly ICustomerActorSystem _customerActorSystem;

        public CustomerController() : this(CustomerActorSystem.Instance)
        {

        }

        public CustomerController(ICustomerActorSystem customerActorSystem)
        {
            _customerActorSystem = customerActorSystem;
        }
        public ActionResult Index()
        {
            return View();
        }

        public async Task<JsonResult> Find(string name)
        {
            var data = await _customerActorSystem.CustomerFinder
                .Ask<CustomerFindResponse>(new CustomerFindRequest { Name = name });
            return Json(data, JsonRequestBehavior.AllowGet);
        }

    }
    public interface ICustomerActorSystem
    {
        IActorRef CustomerFinder { get; }
    }

    public class CustomerActorSystem : ICustomerActorSystem
    {
        public static readonly CustomerActorSystem Instance = new CustomerActorSystem();


        public CustomerActorSystem()
        {
            System = ActorSystem.Create("CustomerActorSystem");
            CustomerFinder = System.ActorOf<CustomerFinder>("CustomerFinder");
        }

        public ActorSystem System { get; }
        public IActorRef CustomerFinder { get; }
    }


    public class CustomerData
    {
        public string Name { get; set; }
    }

    public class CustomerFindRequest
    {
        public string Name { get; set; }
    }

    public class CustomerFindResponse
    {
        public bool Cached { get; set; }
        public CustomerData Data { get; set; }

        public string CreateDate => DateTime.Now.ToString("F");
    }

    public class CustomerService
    {
        public async Task<CustomerData> Find(string name)
        {
            await Task.Delay(10000);
            return new CustomerData { Name = name };
        }
    }

    public class CustomerFinder : ReceiveActor
    {
        public CustomerFinder()
        {
            Setup();
        }
        public void Setup()
        {
            Receive<CustomerFindRequest>(x => FindCustomerData(x));
        }


        public void FindCustomerData(CustomerFindRequest request)
        {
            var actorRef = Context.Child(request.Name);
            if (actorRef is Nobody)
            {
                actorRef = Context.ActorOf<CustomerCache>(request.Name);
            }
            actorRef.Forward(request);
        }

    }


    public class CustomerCache : ReceiveActor
    {

        private CustomerData _customerData;

        public CustomerCache()
        {

            Setup();
        }

        public void Setup()
        {
            ReceiveAsync<CustomerFindRequest>(_ => _customerData == null, FetchCustomerDataFromService);
            Receive<CustomerFindRequest>(_ => _customerData != null, ReturnCustomerDataField);
        }

        public async Task FetchCustomerDataFromService(CustomerFindRequest request)
        {

            _customerData = await new CustomerService().Find(request.Name);

            Sender.Tell(new CustomerFindResponse
            {
                Cached = false,
                Data = _customerData
            });

            // Kill actor 1 minute in the future
            // and with that effectively purging the cached data
            Context.System.Scheduler
                .ScheduleTellOnce(TimeSpan.FromMinutes(1), Self, PoisonPill.Instance, Self);
        }
        public void ReturnCustomerDataField(CustomerFindRequest request)
        {
            Sender.Tell(new CustomerFindResponse
            {
                Cached = true,
                Data = _customerData
            });
        }
    }




}