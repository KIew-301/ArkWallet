using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArkWallet.Presentation.DTOs
{
    public record LoginRequest(string InitData);
    public record LoginResponse(string Token);
}
