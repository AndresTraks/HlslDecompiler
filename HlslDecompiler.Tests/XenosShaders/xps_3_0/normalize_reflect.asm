xps_3_0
config AutoSerialize=false
config AutoResource=false
config PsMaxReg=2
def c252, 0, 0, 0, 0
def c253, 0, 0, 0, 0
def c254, 0, 0, 0, 0
def c255, 16, 0, 0, 0
dcl_texcoord0 r0.xyz
dcl_texcoord1 r1.xyz
/*    0.0 */       alloc colors
/*    0.1 */       exec
/*    2   */          dp3 r0.___w, r1.zxyy, r1.zxyy
/*    3   */          dp3 r1.___w, r0.zxyy, r0.zxyy
              +       rsq r0.___w, r_abs[0].w
/*    4   */          mad r2.xyz_, r0.wwww, r1.xyzz, c0.xyzz
/*    5   */          dp3 r0.___w, r2.zxyy, r2.zxyy
              +       rsq r1.x___, r_abs[1].w
/*    6   */          mul r1.xyz_, r1.xxxx, r0.xyzz
              +       rsq r0.x___, r_abs[0].w
/*    7   */          mul r0.xyz_, r2.xyzz, r0.xxxx
/*    1.0 */       exece
/*    8   */          dp3_sat r0.x___, r1.zxyy, r0.zxyy
/*    9   */          log r0._y__, r0.x
/*   10   */          mulsc r0.x___, c255.x, r0.y
/*   11   */          exp oC0, r0.x
/*    1.1 */       cnop
