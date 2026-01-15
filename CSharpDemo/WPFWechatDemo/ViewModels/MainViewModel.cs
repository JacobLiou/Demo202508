using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using WPFWechatDemo.Models;

namespace WPFWechatDemo.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private Contact? _selectedContact;
        private string _inputMessage = string.Empty;
        private readonly Random _random = new Random();
        private readonly string[] _mockResponses = new[]
        {
            "收到！",
            "好的，明白了",
            "哈哈，有意思",
            "嗯嗯",
            "没问题",
            "稍等一下",
            "OK",
            "好的，我看看",
            "明白了",
            "收到消息"
        };

        public ObservableCollection<Contact> Contacts { get; set; } = new ObservableCollection<Contact>();
        public ObservableCollection<Message> Messages { get; set; } = new ObservableCollection<Message>();

        public Contact? SelectedContact
        {
            get => _selectedContact;
            set
            {
                _selectedContact = value;
                OnPropertyChanged();
                LoadMessages();
            }
        }

        public string InputMessage
        {
            get => _inputMessage;
            set
            {
                _inputMessage = value;
                OnPropertyChanged();
            }
        }

        public ICommand SendMessageCommand { get; }
        public ICommand SelectContactCommand { get; }

        public MainViewModel()
        {
            SendMessageCommand = new RelayCommand(SendMessage, () => !string.IsNullOrWhiteSpace(InputMessage) && SelectedContact != null);
            SelectContactCommand = new RelayCommand<Contact>(SelectContact);
            InitializeContacts();
        }

        private void InitializeContacts()
        {
            Contacts.Add(new Contact
            {
                Id = "1",
                Name = "张三",
                Avatar = "👨",
                LastMessage = "你好",
                LastMessageTime = "10:30"
            });

            Contacts.Add(new Contact
            {
                Id = "2",
                Name = "李四",
                Avatar = "👩",
                LastMessage = "在吗？",
                LastMessageTime = "09:15"
            });

            Contacts.Add(new Contact
            {
                Id = "3",
                Name = "王五",
                Avatar = "🧑",
                LastMessage = "晚上一起吃饭",
                LastMessageTime = "昨天"
            });

            Contacts.Add(new Contact
            {
                Id = "4",
                Name = "赵六",
                Avatar = "👨‍💼",
                LastMessage = "好的，没问题",
                LastMessageTime = "昨天"
            });

            Contacts.Add(new Contact
            {
                Id = "5",
                Name = "工作群",
                Avatar = "👥",
                LastMessage = "明天开会",
                LastMessageTime = "10:00"
            });

            // 默认选择第一个联系人
            if (Contacts.Count > 0)
            {
                SelectedContact = Contacts[0];
            }
        }

        private void SelectContact(Contact? contact)
        {
            SelectedContact = contact;
        }

        private void LoadMessages()
        {
            Messages.Clear();
            if (SelectedContact == null) return;

            // 模拟加载历史消息
            var historyMessages = new[]
            {
                new Message { Content = "你好", IsSent = false, Timestamp = DateTime.Now.AddMinutes(-30) },
                new Message { Content = "你好，有什么事吗？", IsSent = true, Timestamp = DateTime.Now.AddMinutes(-29) },
                new Message { Content = "想咨询一下项目进度", IsSent = false, Timestamp = DateTime.Now.AddMinutes(-28) },
                new Message { Content = "好的，我整理一下发给你", IsSent = true, Timestamp = DateTime.Now.AddMinutes(-27) }
            };

            foreach (var msg in historyMessages)
            {
                msg.ContactId = SelectedContact.Id;
                Messages.Add(msg);
            }
        }

        private async void SendMessage()
        {
            if (string.IsNullOrWhiteSpace(InputMessage) || SelectedContact == null)
                return;

            // 添加发送的消息
            var sentMessage = new Message
            {
                Content = InputMessage,
                IsSent = true,
                Timestamp = DateTime.Now,
                ContactId = SelectedContact.Id
            };

            Messages.Add(sentMessage);

            // 更新联系人的最后消息
            SelectedContact.LastMessage = InputMessage;
            SelectedContact.LastMessageTime = DateTime.Now.ToString("HH:mm");

            // 清空输入框
            var messageToSend = InputMessage;
            InputMessage = string.Empty;

            // 模拟延迟后接收回复消息
            await Task.Delay(1000 + _random.Next(1000, 3000));

            var responseMessage = new Message
            {
                Content = _mockResponses[_random.Next(_mockResponses.Length)],
                IsSent = false,
                Timestamp = DateTime.Now,
                ContactId = SelectedContact.Id
            };

            Messages.Add(responseMessage);

            // 更新联系人的最后消息
            SelectedContact.LastMessage = responseMessage.Content;
            SelectedContact.LastMessageTime = DateTime.Now.ToString("HH:mm");
        }
    }
}


