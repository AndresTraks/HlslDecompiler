xvs_3_0
config AutoSerialize=false
config AutoResource=false
config VsMaxReg=1
config VsResource=1
def c252, 0, 0, 0, 0
def c253, 0, 0, 0, 0
def c254, 0, 0, 0, 0
def c255, 4, 1, 0, 0
dcl_index r0.x
/*    0.0 */       exec
/*    5   */          vfetch r0.xwyz, r0.x, position0
/*    6   */          serialize
                      maxs r0.___w, r0.ww
/*    7   */          max r0.__z_, r0.zzzz, r0.zzzz
              +       maxs r1.___w, r0.yy
/*    8   */          max r0.x___, r0.xxxx, r0.xxxx
              +       sgts r0._y__, -r_abs[0].x
/*    0.1 */       loop i0, L5
                label L2
/*    1.0 */       exec
/*    9   */          max r1.__z_, r0.wwww, r0.wwww
              +       mulsc r1.x___, c255.x, r0.y
/*   10   */          add r0._y__, r0.yyyy, c255.yyyy
              +       maxas r0._, r1.xx
/*   11   */          max r1._y__, r0.zzzz, r0.zzzz
              +       maxs r1.x___, r0.xx
/*   12   */          dp4 r0.x___, r1.wzxy, c[0+a0].wzxy
/*   13   */          dp4 r0.__z_, r1.wzxy, c[1+a0].wzxy
/*   14   */          dp4 r0.___w, r1.wzxy, c[2+a0].wzxy
/*    1.1 */       exec
/*   15   */          dp4 r1.___w, r1.wzxy, c[3+a0].wzxy
/*    2.0 */       endloop i0, L2
                label L5
/*    2.1 */       alloc position
/*    3.0 */       exec
/*   16   */          maxs r0._y__, r1.ww
/*   17   */          max oPos, r0.xzwy, r0.xzwy
/*    3.1 */       alloc interpolators
/*    4.0 */       exece
/*    4.1 */       cnop
