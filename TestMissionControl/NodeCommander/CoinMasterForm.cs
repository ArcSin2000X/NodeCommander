﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Stratis.CoinmasterClient.Client;
using Stratis.CoinmasterClient.Client.Dispatchers;
using Stratis.CoinmasterClient.Config;
using Stratis.CoinmasterClient.Messages;
using Stratis.CoinmasterClient.Network;
using Stratis.CoinmasterClient.Resources;
using Stratis.NodeCommander.Workers;
using Stratis.NodeCommander.Workers.DataStreams;

namespace Stratis.NodeCommander
{
    public partial class CoinMasterForm : Form
    {
        private enum NotificationType
        {
            PerformanceIssue,
            Exception
        }

        private NodeNetwork network;
        private DataView dataGridViewNodesDataView;
        private DataView dataGridViewAgentsDataView;
        private DataView dataGridViewBlockchainDataView;
        private Tulpep.NotificationWindow.PopupNotifier notifier;
        private ClientConnectionManager clientConnectionManager;

        private List<BaseWorker> _workers = new List<BaseWorker>();
        private CryptoIdWorker cryptoIdWorker;

        public CoinMasterForm()
        {
            InitializeComponent();

            NodeCommanderConfig reader = new NodeCommanderConfig();
            network = reader.Config;

            //ToDo: Switch networks
            //...

            //Create pop-up
            notifier = new Tulpep.NotificationWindow.PopupNotifier();
            notifier.Delay = 10000;
            notifier.Click += (sender, eventArgs) =>
            {
                Visible = true;
                BringToFront();
            };

            //Create workers
            cryptoIdWorker = new CryptoIdWorker(10000, new NodeEndpointName("Stratis", "StratisTest"));
            cryptoIdWorker.StateChange += DashboardWorkerStateChanged;
            cryptoIdWorker.DataUpdate += (source, args) => Invoke(new Action<object, CryptoIdDataUpdateEventArgs>(CryptoIdUpdated), source, args);
            _workers.Add(cryptoIdWorker);

            //Start all workers
            foreach (BaseWorker worker in _workers) worker.Start();

            clientConnectionManager = new ClientConnectionManager();
            clientConnectionManager.CreateListOfAgents();
            clientConnectionManager.Session.ConnectionStatusChanged += (connectionAddress, message) => Invoke(new Action<string, string>(AgentDataTableUpdated), connectionAddress, message);
            clientConnectionManager.Session.NodeStatsUpdated += (agentConnection, networkSegment) => Invoke(new Action<ClientConnection, NodeNetwork>(NodeDataUpdated), agentConnection, networkSegment);
            clientConnectionManager.Session.AgentRegistrationUpdated += (agentConnection, agentRegistration) => Invoke(new Action<ClientConnection, AgentRegistration>(AgentRegistrationUpdated), agentConnection, agentRegistration);

            clientConnectionManager.ConnectToAgents();
        }

        private void CryptoIdUpdated(object source, CryptoIdDataUpdateEventArgs arg1)
        {
            DataTable dataTable;
            if (dataGridViewBlockchain.DataSource == null)
            {
                dataTable = BuildBlockchainDataTable();

                dataGridViewBlockchainDataView = new DataView(dataTable, string.Empty, string.Empty, DataViewRowState.CurrentRows);
                dataGridViewBlockchain_Filter();

                dataGridViewBlockchain.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                dataGridViewBlockchain.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
                dataGridViewBlockchain.AutoGenerateColumns = false;

                dataGridViewBlockchain.Columns["Measure"].Width = 10;
                dataGridViewBlockchain.Columns["Value"].Width = 10;
                dataGridViewBlockchain.Columns["Additional"].Width = 60;
            }
            else
            {
                dataTable = dataGridViewBlockchainDataView.Table;
            }

            foreach (PropertyInfo property in arg1.GetType().GetProperties())
            {
                bool found = false;
                foreach (DataRow row in dataTable.Rows)
                {
                    if (row["Measure"].Equals(property.Name))
                    {
                        row["Value"] = property.GetValue(arg1).ToString();

                        found = true;
                        break;
                    }
                }

                if (!found) dataTable.Rows.Add(property.Name, property.GetValue(arg1).ToString());
            }
        }

        private void DashboardWorkerStateChanged(object source, StateChangeHandlerEventArgs args)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<object, StateChangeHandlerEventArgs>(DashboardWorkerStateChanged), source, args);
                return;
            }

            toolStripStatusLabelWorker.Text = args.State.ToString();
            switch (args.State)
            {
                case WorkerState.Idle:
                    toolStripStatusLabelWorker.BackColor = DefaultBackColor;
                    toolStripStatusLabelWorker.ForeColor = Color.Black;
                    break;
                case WorkerState.Stopped:
                    toolStripStatusLabelWorker.BackColor = Color.FromArgb(255, 255, 20, 60);
                    toolStripStatusLabelWorker.ForeColor = Color.White;
                    break;
                case WorkerState.Running:
                    toolStripStatusLabelWorker.BackColor = Color.FromArgb(255, 20, 128, 60);
                    toolStripStatusLabelWorker.ForeColor = Color.White;
                    break;
            }

            //Refresh();
        }

        private DataTable BuildNodeDataTable()
        {
            DataTable nodesData = new DataTable();
            nodesData.Columns.Add("Node", typeof(BlockchainNode));
            nodesData.Columns.Add("Status");
            nodesData.Columns.Add("HeaderHeight");
            nodesData.Columns.Add("ConsensusHeight");
            nodesData.Columns.Add("BlockHeight");
            nodesData.Columns.Add("WalletHeight");
            nodesData.Columns.Add("NetworkHeight");
            nodesData.Columns.Add("Mempool");
            nodesData.Columns.Add("Peers");
            nodesData.Columns.Add("Uptime");
            nodesData.Columns.Add("Messages");

            foreach (string nodeName in network.Nodes.Keys)
            {
                BlockchainNode node = network.Nodes[nodeName];

                object[] rowData = new object[nodesData.Columns.Count];
                rowData[0] = node;
                nodesData.Rows.Add(rowData);
            }

            return nodesData;
        }

        private DataTable BuildAgentDataTable()
        {
            DataTable agentsData = new DataTable();
            agentsData.Columns.Add("Address", typeof(ClientConnection));
            agentsData.Columns.Add("Status");
            agentsData.Columns.Add("Info");
            
            foreach (ClientConnection connection in clientConnectionManager.Session.Clients.Values)
            {
                agentsData.Rows.Add(connection, string.Empty, string.Empty);
            }
            
            return agentsData;
        }

        private DataTable BuildBlockchainDataTable()
        {
            DataTable blockchainData = new DataTable();
            blockchainData.Columns.Add("Measure");
            blockchainData.Columns.Add("Value");
            blockchainData.Columns.Add("Additional");
            
            return blockchainData;
        }

        private void AgentDataTableUpdated(string address, string message)
        {
            DataTable dataTable;
            if (dataGridViewAgents.DataSource == null)
            {
                dataTable = BuildAgentDataTable();

                dataGridViewAgentsDataView = new DataView(dataTable, string.Empty, string.Empty, DataViewRowState.CurrentRows);
                dataGridViewAgents_Filter();

                dataGridViewAgents.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                dataGridViewAgents.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
                dataGridViewAgents.AutoGenerateColumns = false;

                dataGridViewAgents.Columns["Address"].Width = 10;
                dataGridViewAgents.Columns["Status"].Width = 10;
                dataGridViewAgents.Columns["Info"].Width = 60;
            }
            else
            {
                dataTable = dataGridViewAgentsDataView.Table;
            }

            ClientConnection connection = clientConnectionManager.Session.Clients[address];
            foreach (DataRow row in dataTable.Rows)
            {
                ClientConnection tableRecord = (ClientConnection) row["Address"];

                if (tableRecord.Address == address)
                {
                    row["Status"] = connection.State;
                    row["Info"] = message;

                    break;
                }
            }
        }

        private void NodeDataUpdated(ClientConnection clientConnection, NodeNetwork networkSegment)
        {
            int performanceIsues = 0;
            if (dataGridViewNodes.DataSource == null)
            {
                DataTable dataTable = BuildNodeDataTable();

                dataGridViewNodesDataView = new DataView(dataTable, string.Empty, string.Empty, DataViewRowState.CurrentRows);
                dataGridViewNodes_Filter();

                dataGridViewNodes.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                dataGridViewNodes.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
                dataGridViewNodes.AutoGenerateColumns = false;

                dataGridViewNodes.Columns["Node"].Width = 80;
                dataGridViewNodes.Columns["Status"].Width = 80;
                dataGridViewNodes.Columns["HeaderHeight"].Width = 60;
                dataGridViewNodes.Columns["ConsensusHeight"].Width = 60;
                dataGridViewNodes.Columns["BlockHeight"].Width = 60;
                dataGridViewNodes.Columns["WalletHeight"].Width = 60;
                dataGridViewNodes.Columns["NetworkHeight"].Width = 60;
                dataGridViewNodes.Columns["Mempool"].Width = 60;
                dataGridViewNodes.Columns["Peers"].Width = 80;
                dataGridViewNodes.Columns["Uptime"].Width = 80;
                dataGridViewNodes.Columns["Messages"].Width = 80;
            }

            MergeMeasuresIntoNode(network, networkSegment);
            MergeMeasuresIntoDataTable(dataGridViewNodes, network);

            if (notifyAboutPerformanceIssuesToolStripMenuItem.Checked && performanceIsues > 0)
            {
                string message = $"Potential performance issue";
                ShowNotifier("Dashboard", message, NotificationType.PerformanceIssue);
            }
        }

        private void AgentRegistrationUpdated(ClientConnection clientConnection, AgentRegistration agentRegistration)
        {

        }



        private void MergeMeasuresIntoNode(NodeNetwork network, NodeNetwork networkSegment)
        {
            if (networkSegment == null) return;
            network.AgentHealthState = networkSegment.AgentHealthState;

            foreach (string nodeName in network.Nodes.Keys.ToList())
            {
                if (networkSegment.Nodes.ContainsKey(nodeName))
                {
                    network.Nodes[nodeName] = networkSegment.Nodes[nodeName];
                }
            }
        }

        private void MergeMeasuresIntoDataTable(DataGridView grid, NodeNetwork managedNodes)
        {
            if (managedNodes == null) return;

            DataView dataView = ((DataView)grid.DataSource);
            DataTable table = dataView.Table;

            foreach (string fullNodeName in managedNodes.Nodes.Keys)
            {
                foreach (DataRow dataRow in table.Rows)
                {
                    if (managedNodes.Nodes[fullNodeName].Initialized && ((BlockchainNode)dataRow["Node"]).NodeEndpoint.FullNodeName.Equals(fullNodeName))
                    {
                        dataRow["Node"] = managedNodes.Nodes[fullNodeName];
                        dataRow["Status"] = managedNodes.Nodes[fullNodeName].NodeOperationState.State;
                        dataRow["HeaderHeight"] = managedNodes.Nodes[fullNodeName].NodeLogState.HeadersHeight;
                        dataRow["ConsensusHeight"] = managedNodes.Nodes[fullNodeName].NodeLogState.ConsensusHeight;
                        dataRow["BlockHeight"] = managedNodes.Nodes[fullNodeName].NodeLogState.BlockStoreHeight;
                        dataRow["WalletHeight"] = managedNodes.Nodes[fullNodeName].NodeLogState.WalletHeight;
                        dataRow["NetworkHeight"] = managedNodes.Nodes[fullNodeName].NodeOperationState.NetworkHeight;
                        dataRow["Mempool"] = managedNodes.Nodes[fullNodeName].NodeOperationState.MempoolTransactionCount;
                        dataRow["Peers"] = $"In:{managedNodes.Nodes[fullNodeName].NodeOperationState.InboundPeersCount} / Out:{managedNodes.Nodes[fullNodeName].NodeOperationState.OutboundPeersCount}";
                        dataRow["Uptime"] = managedNodes.Nodes[fullNodeName].NodeOperationState.Uptime.ToString("d' days, 'hh':'mm':'ss");
                        dataRow["Messages"] = $"{clientConnectionManager.Session.Database.GetRecordCount(fullNodeName)} records";
                    }
                }
            }
        }


        private void dataGridViewNodes_Filter()
        {
            List<string> filterCriteria = new List<string>();

            if (checkBoxRunningNodesOnly.Checked) filterCriteria.Add($"[Status] = 'Running'");

            dataGridViewNodesDataView.RowFilter = string.Join(" AND ", filterCriteria);
            dataGridViewNodes.DataSource = dataGridViewNodesDataView;
        }
        private void dataGridViewAgents_Filter()
        {
            dataGridViewAgents.DataSource = dataGridViewAgentsDataView;
        }
        private void dataGridViewBlockchain_Filter()
        {
            dataGridViewBlockchain.DataSource = dataGridViewBlockchainDataView;
        }

        private async void dataGridViewNodes_RowStateChanged(object sender, DataGridViewRowStateChangedEventArgs e)
        {
            if (e.StateChanged != DataGridViewElementStates.Selected) return;
            if (dataGridViewNodes.SelectedRows.Count == 0) return;

            BlockchainNode node = (BlockchainNode) dataGridViewNodes.SelectedRows[0].Cells["Node"].Value;
            if (!node.Initialized) return;

            groupBox8.Text = "General - " + node.NodeEndpoint.FullNodeName;
            textBoxCodeDirectory.Text = node.CodeDirectory;
            textBoxProjectDirectory.Text = node.ProjectDirectory;
            textBoxDataDirectory.Text = node.DataDir;
            textBoxNetworkDirectory.Text = node.NetworkDirectory;
            textBoxNodeConfig.Text = node.NodeConfig;
            labelUptime.Text = node.NodeOperationState.Uptime.ToString("d' days, 'hh':'mm':'ss");

            if (node.NodeDeploymentState.DirectoryExists)
            {
                if (node.NodeDeploymentState.MemPoolFileExists)
                    labelMempool.Text = node.NodeDeploymentState.MemPoolFileSize.ToString();
                else labelMempool.Text = "No File";
                if (node.NodeDeploymentState.PeersFileExists)
                    labelPeers.Text = node.NodeDeploymentState.PeersFileExists.ToString();
                else labelPeers.Text = "No File";
            }
            else
            {
                labelMempool.Text = "No Data Dir";
                labelPeers.Text = "No Data Dir";
            }

            //---------------------
            DataTable exceptions = new DataTable();
            exceptions.Columns.Add("Date");
            exceptions.Columns.Add("Thread");
            exceptions.Columns.Add("Level");
            exceptions.Columns.Add("Source");
            exceptions.Columns.Add("Message");
            dataGridViewNodeExceptions.DataSource = exceptions;

            if (node.NodeLogState == null) return;

            
            string resourcePath = Path.Combine(@"C:\Code\TestMissionControl\TestMissionControl\NodeCommander\bin\Debug\Data", node.Resources["nodeCommander.txt"].ToString());
            FileInfo resourceFile = new FileInfo(resourcePath);
            if (!resourceFile.Exists) return;

            FileStream f = new FileStream(resourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            StreamReader reader = new StreamReader(f);
            while (!reader.EndOfStream)
            {
                string line = reader.ReadLine();
                string[] fields = line.Split(new[] {"||"}, StringSplitOptions.None);

                exceptions.Rows.Add(fields);
            }

            f.Close();

        }

        private void button2_Click(object sender, EventArgs e)
        {
            
        }

        private void ShowNotifier(string title, string message, NotificationType type)
        {
            notifier.TitleText = title;
            notifier.ContentText = message;

            switch (type)
            {
                case NotificationType.PerformanceIssue:
                    notifier.BodyColor = Color.FromArgb(255, 215, 228, 188);
                    //notifier.Image = Resources.Browser_aol;
                    notifier.ImagePadding = new Padding(5);
                    break;
                case NotificationType.Exception:
                    notifier.BodyColor = Color.FromArgb(255, 230, 185, 184);
                    //notifier.Image = Resources.Bug;
                    notifier.ImagePadding = new Padding(5);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }

            notifier.Popup();
        }

        private void buttonDeployFiles_Click(object sender, EventArgs e)
        {
            if (dataGridViewNodes.SelectedRows.Count == 0) return;

            foreach (DataGridViewRow row in dataGridViewNodes.SelectedRows)
            {
                BlockchainNode node = (BlockchainNode)row.Cells["Node"].Value;
                var agent = clientConnectionManager.GetAgent(node.Agent);

                ResourceDeploymentDispatcher dispatcher = (ResourceDeploymentDispatcher)agent.Dispatchers[MessageType.DeployFile];
                dispatcher.DeployFile(network, ResourceScope.Node, node.NodeEndpoint.FullNodeName);
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            if (dataGridViewNodes.SelectedRows.Count == 0) return;

            foreach (DataGridViewRow row in dataGridViewNodes.SelectedRows)
            {
                BlockchainNode node = (BlockchainNode)row.Cells["Node"].Value;
                var agent = clientConnectionManager.GetAgent(node.Agent);

                NodeActionDispatcher dispatcher = (NodeActionDispatcher)agent.Dispatchers[MessageType.ActionRequest];
                dispatcher.StartNode(node);
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            if (dataGridViewNodes.SelectedRows.Count == 0) return;

            foreach (DataGridViewRow row in dataGridViewNodes.SelectedRows)
            {
                BlockchainNode node = (BlockchainNode)row.Cells["Node"].Value;
                var agent = clientConnectionManager.GetAgent(node.Agent);

                NodeActionDispatcher dispatcher = (NodeActionDispatcher)agent.Dispatchers[MessageType.ActionRequest];
                dispatcher.StopNode(node);
            }
        }

        private void button11_Click(object sender, EventArgs e)
        {
            if (dataGridViewNodes.SelectedRows.Count == 0) return;

            foreach (DataGridViewRow row in dataGridViewNodes.SelectedRows)
            {
                BlockchainNode node = (BlockchainNode)row.Cells["Node"].Value;
                var agent = clientConnectionManager.GetAgent(node.Agent);

                NodeActionDispatcher dispatcher = (NodeActionDispatcher)agent.Dispatchers[MessageType.ActionRequest];
                dispatcher.RemoveFile(node, "$NetworkDirectory\\hello.txt");
            }
        }

        private void dataGridViewNodes_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            BlockchainNode node = (BlockchainNode)dataGridViewNodes.Rows[e.RowIndex].Cells[0].Value;
            if (!node.Initialized) return;

            int maxHeight;
            int headersHeight;
            if (!int.TryParse(node.NodeLogState.HeadersHeight, out headersHeight)) headersHeight = 0;

            int consensusHeight;
            if (!int.TryParse(node.NodeLogState.ConsensusHeight, out consensusHeight)) consensusHeight = 0;

            int blockstoreHeight;
            if (!int.TryParse(node.NodeLogState.BlockStoreHeight, out blockstoreHeight)) blockstoreHeight = 0;

            int walletHeight;
            if (!int.TryParse(node.NodeLogState.WalletHeight, out walletHeight)) walletHeight = -1;

            int networkHeight = node.NodeOperationState.NetworkHeight;

            maxHeight = Math.Max(headersHeight, consensusHeight);
            maxHeight = Math.Max(maxHeight, blockstoreHeight);
            maxHeight = Math.Max(maxHeight, walletHeight);
            maxHeight = Math.Max(maxHeight, networkHeight);

            if (walletHeight == -1) walletHeight = maxHeight;

            
            if (e.ColumnIndex == 2 && headersHeight < maxHeight)
            {
                e.CellStyle.ForeColor = Color.Crimson;
            }
            else if (e.ColumnIndex == 3 && consensusHeight < maxHeight)
            {
                e.CellStyle.ForeColor = Color.Crimson;
            }
            else if (e.ColumnIndex == 4 && blockstoreHeight < maxHeight)
            {
                e.CellStyle.ForeColor = Color.Crimson;
            }
            else if (e.ColumnIndex == 5 && walletHeight < maxHeight)
            {
                e.CellStyle.ForeColor = Color.Crimson;
            }
            else if (e.ColumnIndex == 6 && networkHeight < maxHeight)
            {
                e.CellStyle.ForeColor = Color.Crimson;
            }
            else
            {
                e.CellStyle.ForeColor = Color.Black;
            }
            


        }
    }
}
