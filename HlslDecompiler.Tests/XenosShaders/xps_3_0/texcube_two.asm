xps_3_0
config AutoSerialize=false
config AutoResource=false
config PsMaxReg=2
def c252, 0, 0, 0, 0
def c253, 0, 0, 0, 0
def c254, 0, 0, 0, 0
def c255, 1.5, 0.75, 0, 0
dcl_texcoord0 r0.xyz
dcl_texcoord1 r1.xyz
/*    0.0 */       exec
/*    3   */          dp3 r2._y__, r0.zxyy, r0.zxyy
/*    4   */          dp3 r0.___w, r0.zxyy, r1.zxyy
              +       maxs r0._, r0.xx
/*    5   */          mul r2.__z_, r0.wwww, c255.yyyy
              +       adds_prev r2.x___, r1.y
/*    6   */          add r2.x__w, r2.xyyy, r2.xzzz
/*    7   */          mad r2.xyz_, -r2.xxxx, r1.xyzz, r0.xyzz
/*    8   */          mul r1.xyz_, r2.wwww, r1.xyzz
/*    0.1 */       exec
/*    9   */          mad r1.xyz_, r0.xyzz, c255.yyyy, -r1.xyzz
/*   10   */          cube r0, r2.zzxy, r2.yxzz
/*   11   */          maxs r2.__z_, r0.ww
/*   12   */          cube r1, r1.zzxy, r1.yxzz
              +       rcp r2.x___, r_abs[0].z
/*   13   */          mad r2.xy__, r0.yxxx, r2.xxxx, c255.xxxx
/*   14   */          max r0.__z_, r1.wwww, r1.wwww
              +       rcp r0.x___, r_abs[1].z
/*    1.0 */       exec
/*   15   */          mad r0.xy__, r1.yxxx, r0.xxxx, c255.xxxx
/*   16   */          tfetchCube r1, r2.xyz, tf0
/*   17   */          tfetchCube r0, r0.xyz, tf1
/*    1.1 */       alloc colors
/*    2.0 */       exece
/*   18   */          mul oC0, r1, r0
/*    2.1 */       cnop
