using System;
using System.Threading;

class Program
{
    /*
        Este programa simula o funcionamento do Algoritmo do Banqueiro
        utilizando múltiplas threads.

        Cada cliente é representado por uma thread. Os clientes solicitam
        e liberam recursos aleatoriamente. O banqueiro só aceita uma
        solicitação se ela mantiver o sistema em estado seguro.
    */

    // Quantidade fixa de clientes, para testes.
    private const int NumberOfCustomers = 5;

    // Quantidade de tipos de recursos.
    // Esse valor será definido de acordo com os argumentos passados na execução.
    private static int NumberOfResources;

    /*
        Vetor available:
        Guarda a quantidade disponível de cada tipo de recurso.
    */
    private static int[] available = Array.Empty<int>();

    /*
        Matriz maximum:
        Guarda a demanda máxima de cada cliente para cada tipo de recurso.

        maximum[cliente, recurso]
    */
    private static int[,] maximum = new int[NumberOfCustomers, 0];

    /*
        Matriz allocation:
        Guarda a quantidade de recursos atualmente alocada para cada cliente.

        allocation[cliente, recurso]
    */
    private static int[,] allocation = new int[NumberOfCustomers, 0];

    /*
        Matriz need:
        Guarda a necessidade restante de cada cliente.

        need = maximum - allocation
    */
    private static int[,] need = new int[NumberOfCustomers, 0];

    /*
        Objeto utilizado como lock.

        Como várias threads acessam e modificam as mesmas estruturas
        compartilhadas, é preciso proteger as operações para evitar
        condição de corrida.
    */
    private static readonly object bankerLock = new object();

    /*
        Cada thread terá seu próprio gerador de números aleatórios.

        Isso evita problemas causados pelo uso do mesmo objeto Random
        simultaneamente por várias threads.
    */
    private static readonly ThreadLocal<Random> random = new ThreadLocal<Random>(
        () => new Random(Guid.NewGuid().GetHashCode())
    );

    static void Main(string[] args)
    {
        /*
            O programa deve receber a quantidade de recursos pela linha
            de comando.
        */

        if (args.Length == 0)
        {
            Console.WriteLine("Erro: informe a quantidade de recursos de cada tipo.");
            Console.WriteLine("Exemplo de execução:");
            Console.WriteLine("dotnet run -- 10 5 7");
            return;
        }

        NumberOfResources = args.Length;

        // Inicialização das estruturas com base na quantidade de recursos informada.
        available = new int[NumberOfResources];
        maximum = new int[NumberOfCustomers, NumberOfResources];
        allocation = new int[NumberOfCustomers, NumberOfResources];
        need = new int[NumberOfCustomers, NumberOfResources];

        // Leitura e validação dos recursos recebidos pela linha de comando.
        for (int i = 0; i < NumberOfResources; i++)
        {
            if (!int.TryParse(args[i], out available[i]) || available[i] < 0)
            {
                Console.WriteLine("Erro: todos os recursos devem ser números inteiros maiores ou iguais a zero.");
                return;
            }
        }

        // Inicializa as matrizes maximum, allocation e need.
        InitializeMaximumAndNeed();

        Console.WriteLine("======================================");
        Console.WriteLine("     Algoritmo do Banqueiro em C#");
        Console.WriteLine("======================================");
        Console.WriteLine();

        Console.WriteLine("Estado inicial do sistema:");
        PrintState();

        /*
            Criação das threads dos clientes.

            Cada cliente executa sua própria rotina, solicitando e liberando
            recursos de forma independente.
        */
        Thread[] customers = new Thread[NumberOfCustomers];

        for (int i = 0; i < NumberOfCustomers; i++)
        {
            int customerId = i;

            customers[i] = new Thread(() => CustomerRoutine(customerId));
            customers[i].Name = $"Cliente {customerId}";
            customers[i].Start();
        }

        /*
            O método Join faz com que o programa principal aguarde a conclusão
            de todas as threads antes de finalizar.
        */
        foreach (Thread customer in customers)
        {
            customer.Join();
        }

        Console.WriteLine();
        Console.WriteLine("======================================");
        Console.WriteLine("Execução finalizada.");
        Console.WriteLine("Estado final do sistema:");
        PrintState();
    }

    private static void InitializeMaximumAndNeed()
    {
        /*
            A matriz maximum é inicializada aleatoriamente.

            Cada cliente recebe uma demanda máxima para cada recurso.
            Essa demanda nunca ultrapassa o total inicial disponível daquele recurso.

            A matriz allocation começa com zero, pois nenhum recurso foi
            alocado inicialmente.

            A matriz need começa igual à matriz maximum.
        */
        for (int customer = 0; customer < NumberOfCustomers; customer++)
        {
            for (int resource = 0; resource < NumberOfResources; resource++)
            {
                maximum[customer, resource] = random.Value!.Next(1, available[resource] + 1);
                allocation[customer, resource] = 0;
                need[customer, resource] = maximum[customer, resource];
            }
        }
    }

    private static void CustomerRoutine(int customerId)
    {
        /*
            Para fins de teste e visualização, usamos um número
            limitado de ciclos. Isso evita que o programa fique executando
            infinitamente no terminal.

            Para loop contínuo, é preciso trocar o for abaixo por:
            while (true)
        */
        for (int cycle = 1; cycle <= 8; cycle++)
        {
            Console.WriteLine();
            Console.WriteLine($"Cliente {customerId} iniciou o ciclo {cycle}.");

            // Gera uma solicitação aleatória dentro do limite da necessidade do cliente.
            int[] request = GenerateRandomRequest(customerId);

            if (HasAnyValue(request))
            {
                Console.WriteLine($"Cliente {customerId} solicitando recursos: [{string.Join(", ", request)}]");

                int requestResult = RequestResources(customerId, request);

                if (requestResult == 0)
                {
                    Console.WriteLine($"Solicitação do cliente {customerId} ACEITA pelo banqueiro.");
                }
                else
                {
                    Console.WriteLine($"Solicitação do cliente {customerId} NEGADA pelo banqueiro.");
                }
            }
            else
            {
                Console.WriteLine($"Cliente {customerId} não solicitou recursos neste ciclo.");
            }

            // Pequena pausa para deixar a execução das threads mais visível.
            Thread.Sleep(random.Value!.Next(500, 1200));

            // Gera uma liberação aleatória com base nos recursos alocados ao cliente.
            int[] release = GenerateRandomRelease(customerId);

            if (HasAnyValue(release))
            {
                Console.WriteLine($"Cliente {customerId} liberando recursos: [{string.Join(", ", release)}]");

                int releaseResult = ReleaseResources(customerId, release);

                if (releaseResult == 0)
                {
                    Console.WriteLine($"Cliente {customerId} liberou recursos com sucesso.");
                }
                else
                {
                    Console.WriteLine($"Falha ao liberar recursos do cliente {customerId}.");
                }
            }
            else
            {
                Console.WriteLine($"Cliente {customerId} não possui recursos para liberar neste ciclo.");
            }

            Thread.Sleep(random.Value!.Next(500, 1200));
        }
    }

    private static int[] GenerateRandomRequest(int customerId)
    {
        /*
            A solicitação não pode ultrapassar o valor da matriz need,
            pois o cliente não pode pedir mais do que ainda precisa.
        */
        int[] request = new int[NumberOfResources];

        lock (bankerLock)
        {
            for (int resource = 0; resource < NumberOfResources; resource++)
            {
                int maxRequest = need[customerId, resource];

                if (maxRequest == 0)
                {
                    request[resource] = 0;
                }
                else
                {
                    request[resource] = random.Value!.Next(0, maxRequest + 1);
                }
            }
        }

        return request;
    }

    private static int[] GenerateRandomRelease(int customerId)
    {
        // O cliente só pode liberar recursos que já estão alocados a ele.

        int[] release = new int[NumberOfResources];

        lock (bankerLock)
        {
            for (int resource = 0; resource < NumberOfResources; resource++)
            {
                int allocated = allocation[customerId, resource];

                if (allocated == 0)
                {
                    release[resource] = 0;
                }
                else
                {
                    release[resource] = random.Value!.Next(0, allocated + 1);
                }
            }
        }

        return release;
    }

    private static int RequestResources(int customerNum, int[] request)
    {
        lock (bankerLock)
        {
            Console.WriteLine();
            Console.WriteLine($"Analisando solicitação do cliente {customerNum}...");

            if (!IsRequestValid(customerNum, request))
            {
                Console.WriteLine("Solicitação inválida: ultrapassa a necessidade do cliente ou os recursos disponíveis.");
                return -1;
            }

            // Simula a alocação dos recursos.
            // Verifica se o sistema continua seguro.

            AllocateResources(customerNum, request);

            if (IsSafeState(out string safeSequence))
            {
                Console.WriteLine("Estado seguro encontrado.");
                Console.WriteLine($"Sequência segura: {safeSequence}");
                PrintState();
                return 0;
            }

            // Se o sistema não ficar seguro, a alocação simulada é desfeita.
            // Assim, o estado anterior é restaurado.
            
            RollbackAllocation(customerNum, request);

            Console.WriteLine("Estado inseguro detectado. Solicitação negada e alterações desfeitas.");
            return -1;
        }
    }

    private static int ReleaseResources(int customerNum, int[] release)
    {
        lock (bankerLock)
        {
            for (int resource = 0; resource < NumberOfResources; resource++)
            {
                if (release[resource] < 0 || release[resource] > allocation[customerNum, resource])
                {
                    return -1;
                }
            }

            for (int resource = 0; resource < NumberOfResources; resource++)
            {
                allocation[customerNum, resource] -= release[resource];
                available[resource] += release[resource];
                need[customerNum, resource] += release[resource];
            }

            Console.WriteLine();
            Console.WriteLine($"Recursos liberados pelo cliente {customerNum}.");
            PrintState();

            return 0;
        }
    }

    private static bool IsRequestValid(int customerNum, int[] request)
    {
        /*
            A solicitação precisa atender duas condições:

            1. request <= need
               O cliente não pode pedir mais do que sua necessidade restante.

            2. request <= available
               O sistema precisa ter recursos disponíveis para atender ao pedido.
        */
        for (int resource = 0; resource < NumberOfResources; resource++)
        {
            if (request[resource] < 0)
            {
                return false;
            }

            if (request[resource] > need[customerNum, resource])
            {
                return false;
            }

            if (request[resource] > available[resource])
            {
                return false;
            }
        }

        return true;
    }

    private static void AllocateResources(int customerNum, int[] request)
    {
        /*
            Simula a concessão dos recursos solicitados.

            available diminui
            allocation aumenta
            need diminui
        */
        for (int resource = 0; resource < NumberOfResources; resource++)
        {
            available[resource] -= request[resource];
            allocation[customerNum, resource] += request[resource];
            need[customerNum, resource] -= request[resource];
        }
    }

    private static void RollbackAllocation(int customerNum, int[] request)
    {
        /*
            Desfaz a alocação simulada quando o sistema ficaria inseguro.

            available volta a aumentar
            allocation volta a diminuir
            need volta a aumentar
        */
        for (int resource = 0; resource < NumberOfResources; resource++)
        {
            available[resource] += request[resource];
            allocation[customerNum, resource] -= request[resource];
            need[customerNum, resource] += request[resource];
        }
    }

    private static bool IsSafeState(out string safeSequence)
    {
        /*
            Algoritmo de segurança.

            Verifica se existe uma ordem em que todos os clientes
            conseguem terminar sua execução com os recursos disponíveis.

            Se existir essa ordem, o sistema está em estado seguro.
            Se não existir, o sistema está em estado inseguro.
        */

        int[] work = new int[NumberOfResources];
        bool[] finish = new bool[NumberOfCustomers];
        int[] sequence = new int[NumberOfCustomers];
        int sequenceIndex = 0;

        // O vetor work começa como uma cópia dos recursos disponíveis.
        for (int resource = 0; resource < NumberOfResources; resource++)
        {
            work[resource] = available[resource];
        }

        bool foundCustomer;

        do
        {
            foundCustomer = false;

            for (int customer = 0; customer < NumberOfCustomers; customer++)
            {
                /*
                    Um cliente pode terminar se:
                    - ainda não terminou
                    - sua necessidade restante é menor ou igual aos recursos em work
                */
                if (!finish[customer] && CanFinish(customer, work))
                {
                    /*
                        Quando esse cliente "termina", ele devolve ao sistema
                        todos os recursos que estavam alocados a ele.
                    */
                    for (int resource = 0; resource < NumberOfResources; resource++)
                    {
                        work[resource] += allocation[customer, resource];
                    }

                    finish[customer] = true;
                    sequence[sequenceIndex] = customer;
                    sequenceIndex++;

                    foundCustomer = true;
                }
            }

        } while (foundCustomer);

        // Se algum cliente não conseguiu terminar, o sistema não está
        // em estado seguro.
        
        for (int customer = 0; customer < NumberOfCustomers; customer++)
        {
            if (!finish[customer])
            {
                safeSequence = "não existe sequência segura";
                return false;
            }
        }

        safeSequence = string.Join(" -> ", sequence);
        return true;
    }

    private static bool CanFinish(int customer, int[] work)
    {
        // Verifica se a necessidade restante do cliente é menor ou igual
        // aos recursos temporariamente disponíveis no vetor work.
        
        for (int resource = 0; resource < NumberOfResources; resource++)
        {
            if (need[customer, resource] > work[resource])
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasAnyValue(int[] vector)
    {
        // Verifica se um vetor possui pelo menos um valor maior que zero.
        // Isso evita exibir solicitações ou liberações totalmente vazias.

        foreach (int value in vector)
        {
            if (value > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static void PrintState()
    {
        // Exibe o estado atual do sistema.
  
        Console.WriteLine();
        Console.WriteLine("Estado atual do sistema:");
        Console.WriteLine($"Available: [{string.Join(", ", available)}]");

        Console.WriteLine();
        Console.WriteLine("Maximum:");
        PrintMatrix(maximum);

        Console.WriteLine();
        Console.WriteLine("Allocation:");
        PrintMatrix(allocation);

        Console.WriteLine();
        Console.WriteLine("Need:");
        PrintMatrix(need);

        Console.WriteLine("--------------------------------------");
    }

    private static void PrintMatrix(int[,] matrix)
    {
        /*
            Exibe uma matriz no formato:
            Cliente 0: [x, y, z]
            Cliente 1: [x, y, z]
        */
        for (int customer = 0; customer < NumberOfCustomers; customer++)
        {
            Console.Write($"Cliente {customer}: [");

            for (int resource = 0; resource < NumberOfResources; resource++)
            {
                Console.Write(matrix[customer, resource]);

                if (resource < NumberOfResources - 1)
                {
                    Console.Write(", ");
                }
            }

            Console.WriteLine("]");
        }
    }
}