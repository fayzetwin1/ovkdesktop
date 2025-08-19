using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ovkdesktop.Models;

namespace ovkdesktop.Converters
{
    public class AttachmentTemplateSelector : DataTemplateSelector
    {
        public DataTemplate PhotoTemplate { get; set; }
        public DataTemplate VideoTemplate { get; set; }
        public DataTemplate GifTemplate { get; set; }
        public DataTemplate OtherTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is Attachment attachment)
            {
                if (attachment.IsPhoto)
                {
                    return PhotoTemplate;
                }
                if (attachment.IsVideo)
                {
                    return VideoTemplate;
                }
                if (attachment.IsGif)
                {
                    return GifTemplate;
                }
            }
            return OtherTemplate;
        }
    }
}
