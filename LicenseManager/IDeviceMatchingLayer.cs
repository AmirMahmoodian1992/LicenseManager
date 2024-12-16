using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LicenseManager
{
    public interface IDeviceMatchingLayer
    {
        ValidationResult Match(LicenseRequest request);
    }

}
