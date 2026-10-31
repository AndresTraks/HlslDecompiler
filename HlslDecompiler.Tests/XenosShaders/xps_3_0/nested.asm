xps_3_0
config AutoSerialize=false
config AutoResource=false
config PsMaxReg=3
def c252, 0, 0, 0, 0
def c253, 0, 0, 0, 0
def c254, 0, 0, 0, 0
def c255, 1, 0, 0, 0
dcl_texcoord0 r0
/*    0.0 */       exec
/*    5   */          sgt r3, -r_abs[0].xxxx, c255.yyyy
/*    6   */          sgt r2, -r_abs[0].xxxx, c255.yyyy
              +       sgts r1.__z_, -r_abs[0].x
/*    0.1 */       loop i16, L8
                label L2
/*    1.0 */       exec
/*    7   */          sgts r1._y__, -r_abs[0].x
/*    1.1 */       loop i17, L6
                label L4
/*    2.0 */       exec
/*    8   */          add r1.x___, r1.zzzz, r1.yyyy
/*    9   */          mul r2, r0.wzyx, c0.wzyx
              +       addsc r1._y__, c255.x, r1.y
/*   10   */          mad r3, r2.wzyx, r1.xxxx, r3
/*    2.1 */       endloop i17, L4
                label L6
/*    3.0 */       exec
/*   11   */          max r2, r3.wzyx, r3.wzyx
              +       addsc r1.__z_, c255.x, r1.z
/*    3.1 */       endloop i16, L2
                label L8
/*    4.0 */       alloc colors
/*    4.1 */       exece
/*   12   */          max oC0, r2.wzyx, r2.wzyx
