xvs_3_0
config AutoSerialize=false
config AutoResource=false
config VsMaxReg=1
config VsResource=1
def c252, 0, 0, 0, 0
def c253, 0, 0, 0, 0
def c254, 0.15915494, 1.442695, 0.5, -3.1415927
def c255, 6.2831855, 0, 0, 0
dcl_index r0.x
/*    0.0 */       exec
/*    3   */          vfetch r0.zwxy, r0.x, position0
/*    0.1 */       alloc position
/*    1.0 */       exec
/*    4   */          mul r0.xy__, r0.xyyy, c254.xyyy
/*    5   */          exp oPos.___w, r0.y
/*    6   */          addsc r1.x___, c254.z, r0.x
/*    7   */          frcs r1.x___, r1.x
/*    8   */          mad r0.x___, r1.xxxx, c255.xxxx, c254.wwww
/*    9   */          frc oPos.xy__, r0.zwww
              +       sin oPos.__z_, r0.x
/*    1.1 */       alloc interpolators
/*    2.0 */       exece
/*    2.1 */       cnop
