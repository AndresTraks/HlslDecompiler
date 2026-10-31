xvs_3_0
config AutoSerialize=false
config AutoResource=false
config VsMaxReg=2
config VsResource=1
def c252, 0, 0, 0, 0
def c253, 0.27852115, 0.5, 6.2831855, -3.1415927
def c254, -1, 7, 9, 0.5
def c255, 0.25, 1.5, 2.5, 3.5
dcl_index r0.x
/*    0.0 */       exec
/*    3   */          vfetch r2, r0.x, position0
/*    0.1 */       alloc position
/*    1.0 */       exec
/*    4   */          mul r0.xy__, r2.xxxx, c253.xyyy
/*    5   */          addsc r0.__z_, c254.w, r0.x
/*    6   */          frcs r0.__z_, r0.z
/*    7   */          mad r0.__z_, r0.zzzz, c253.zzzz, c253.wwww
/*    8   */          mul r1._yzw, r2.wwww, c254.xxyz
              +       sin r1.x___, r0.z
/*    9   */          mad r2, r2.wyzw, c255, r1
/*    1.1 */       exec
/*   10   */          add oPos.x___, r2.xxxx, r0.yyyy
/*   11   */          add oPos._yzw, r2.yyzw, r1.xxxx
/*    2.0 */       alloc interpolators
/*    2.1 */       exece
