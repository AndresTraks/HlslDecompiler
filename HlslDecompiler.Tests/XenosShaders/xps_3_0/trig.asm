xps_3_0
config AutoSerialize=false
config AutoResource=false
config PsMaxReg=4
def c252, 0, 0, 0, 0
def c253, 0.999866, 0.15915494, 0.5, 1.5707964
def c254, -0.085133, -3.1415927, 0.180141, -0.3302995
def c255, 0.0208351, 6.2831855, -2, -3.1415927
dcl_texcoord0 r0
/*    0.0 */       alloc colors
/*    0.1 */       exec
/*    3   */          mins r1._y__, r_abs[0].wx
/*    4   */          sgt r1.___w, r_abs[0].wwww, r_abs[0].xxxx
              +       maxs r1.x___, r_abs[0].wx
/*    5   */          mad r2.xyz_, r0.xyzz, c253.yyyy, c253.zzzz
/*    6   */          frc r2.xyz_, r2.xyzz
              +       rcp r1.x___, r1.x
/*    7   */          mul r1.x___, r1.yyyy, r1.xxxx
              +       maxs r4.x___, r0.wx
/*    8   */          mul r2.___w, r1.xxxx, r1.xxxx
              +       mins r1.__z_, r0.wx
/*    1.0 */       exec
/*    9   */          mad r3, r2, c255.yyyx, c254.yyyx
/*   10   */          sin oC0.x___, r3.x
/*   11   */          sgt r1._y__, -r0.xxxx, r0.xxxx
              +       sin r0.__z_, r3.z
/*   12   */          sgt r1.__z_, -r1.zzzz, r1.zzzz
              +       cos r0.___w, r3.z
/*   13   */          mad r0.x___, r2.wwww, r3.wwww, c254.zzzz
/*   14   */          mad r0.x___, r2.wwww, r0.xxxx, c254.wwww
/*    1.1 */       exec
/*   15   */          sge r0._y__, r4.xxxx, -r4.xxxx
              +       rcp r0.___w, r0.w
/*   16   */          mul oC0.__z_, r0.wwww, r0.zzzz
              +       cos oC0._y__, r3.y
/*   17   */          mad r0.x___, r2.wwww, r0.xxxx, c253.xxxx
/*   18   */          mul r1.x_z_, r1.xzzz, r0.xyyy
/*   19   */          mul r0.xy__, r1.xyyy, c255.zwww
/*   20   */          addsc r0.__z_, c253.w, r0.x
/*    2.0 */       exece
/*   21   */          mad r0.__z_, r0.zzzz, r1.wwww, r1.xxxx
/*   22   */          add r0.x___, r0.zzzz, r0.yyyy
/*   23   */          adds r0._y__, r0.xx
/*   24   */          mad oC0.___w, r1.zzzz, -r0.yyyy, r0.xxxx
/*    2.1 */       cnop
