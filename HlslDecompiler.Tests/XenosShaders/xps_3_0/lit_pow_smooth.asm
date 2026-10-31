xps_3_0
config AutoSerialize=false
config AutoResource=false
config PsMaxReg=4
def c252, 0, 0, 0, 0
def c253, 0, 0, 0, 0
def c254, 0, 0, 0, 0
def c255, 1, 2, 3, 0
dcl_texcoord0 r0
/*    0.0 */       alloc colors
/*    0.1 */       exec
/*    2   */          log r1.x___, r_abs[0].x
/*    3   */          log r1.__z_, r_abs[0].w
/*    4   */          log r1.___w, r_abs[0].z
/*    5   */          add r2._y__, c0.wwww, -c0.zzzz
              +       log r1._y__, r_abs[0].y
/*    6   */          add r3, r0, -c0.zzzz
              +       mulsc r2.x___, c0.x, r1.y
/*    7   */          mul r1, r1.xywz, c0.yyyy
              +       rcp r2._y__, r2.y
/*    1.0 */       exec
/*    8   */          mul_sat r3, r3, r2.yyyy
              +       exp r4.x___, r2.x
/*    9   */          mad r2, -r3, c255.yyyy, c255.zzzz
/*   10   */          sgt r4._yz_, r0.yyxx, -r0.yyxx
              +       exp r1._y__, r1.y
/*   11   */          mul r4._y__, r4.zzzz, r4.yyyy
              +       exp r1.__z_, r1.z
/*   12   */          mul r0.x___, r4.zzzz, r0.xxxx
              +       exp r0._y__, r1.x
/*   13   */          mul r0.___w, r4.yyyy, r4.xxxx
              +       exp r0.__z_, r1.w
/*    1.1 */       exece
/*   14   */          mul r3, r3, r3
              +       addsc r1.x___, c255.x, r0.y
/*   15   */          add r1.__zw, r0.xxxw, r1.yyyz
              +       addsc r1._y__, c255.x, r0.z
/*   16   */          mad oC0, r3, r2, r1.xzwy
