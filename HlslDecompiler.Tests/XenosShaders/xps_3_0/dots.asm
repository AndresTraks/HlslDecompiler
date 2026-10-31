xps_3_0
config AutoSerialize=false
config AutoResource=false
config PsMaxReg=3
def c252, 0, 0, 0, 0
def c253, 0, 0, 0, 0
def c254, 0, 0, 0, 0
def c255, 0, 0, 0, 0
dcl_texcoord0 r0
/*    0.0 */       alloc colors
/*    0.1 */       exec
/*    2   */          dp4 r2.x___, r0.wzxy, c0.wzxy
/*    3   */          dp3 r2._y__, r0.zxyy, c1.zxyy
/*    4   */          dp2add r1._y__, r0.xyyy, r0.xyyy, c255.xxxx
/*    5   */          add r3.xyz_, r0.xyzz, -c1.xyzz
/*    6   */          dp4 r1.x___, r0.zxyw, r0.zxyw
/*    7   */          dp3 r1.___w, r3.zxyy, r3.zxyy
              +       rsq r1.x___, r_abs[1].x
/*    1.0 */       exece
/*    8   */          mul r2.__zw, r1.xxxx, r0.zzzw
              +       sqrt r1.__z_, r_abs[1].y
/*    9   */          mul r1.xy__, r1.xxxx, r0.xyyy
              +       sqrt r1.___w, r_abs[1].w
/*   10   */          add oC0, r2, r1
/*    1.1 */       cnop
