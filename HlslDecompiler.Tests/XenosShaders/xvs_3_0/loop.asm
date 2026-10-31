xvs_3_0
config AutoSerialize=false
config AutoResource=false
config VsMaxReg=1
config VsResource=1
dcl_index r0.x
/*    0.0 */       exec
/*    4   */          vfetch r1, r0.x, position0
/*    5   */          serialize
                      max r0.xy__, r1.xxxx, r1.xxxx
/*    0.1 */       loop i0, L4
                label L2
/*    1.0 */       exec
/*    6   */          mad r0.xy__, r1.xxxx, c[0+aL].xxxx, r0.xxxx
/*    7   */          mad r1._yzw, r1.xxxx, c[0+aL].yyzw, r1.yyzw
/*    1.1 */       endloop i0, L2
                label L4
/*    2.0 */       alloc position
/*    2.1 */       exec
/*    8   */          maxs r1.x___, r0.yy
/*    9   */          max oPos, r1, r1
/*    3.0 */       alloc interpolators
/*    3.1 */       exece
