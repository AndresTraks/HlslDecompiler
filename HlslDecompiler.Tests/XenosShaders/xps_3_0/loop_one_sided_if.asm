xps_3_0
config AutoSerialize=false
config AutoResource=false
config PsMaxReg=2
def c252, 0, 0, 0, 0
def c253, 0, 0, 0, 0
def c254, -0.5, 1, 0, 0
def c255, 0.25, 0.5, 0.75, 0
dcl_texcoord0 r0
/*    0.0 */       exec    // PredicateClean=false
/*    4   */          sge r1.xy__, r0.zyyy, c255.wwww
              +       maxs r0._, r0.xx
/*    5   */          mul r1._y__, r1.xxxx, r1.yyyy
              +       adds_prev r1.x___, c254.y
/*    6   */          cndeq r2.x___, r1.yyyy, r0.xxxx, r1.xxxx
/*    7   */          setp_ne_push r1.x___, c255.wwww, r1.yyyy
              +       sgts r1.__z_, -r_abs[0].x
/*    8   */     (p0) maxs r2.__z_, r0.zz
/*    9   */          setp_rstr r2.___w, r1.x
/*    0.1 */       loop i31, L5
                label L2
/*    1.0 */       exec    // PredicateClean=false
/*   10   */          setp_ne_push r2.___w, r2.wwww, r1.yyyy
/*   11   */     (p0) max r1.___w, r0.wwww, r0.wwww
              +  (p0) maxs r2._y__, r2.zz
/*   12   */     (p0) add r2.x_z_, r2.xyyy, c254.xyyy
              +  (p0) maxs r1.x___, r0.xx
/*   13   */     (p0) maxs r1._y__, r0.yy
/*   14   */     (p0) add r0.xy_w, r1.xyww, c255.xyzz
/*   15   */     (p0) dp3 r1.x___, r1.wxyy, c255.zxyy
              +  (p0) sgts r1._y__, r2.x
/*    1.1 */       exec    // PredicateClean=false
/*   16   */     (p0) mad r1.__z_, r1.xxxx, r2.yyyy, r1.zzzz
/*   17   */          setp_pop r2.___w, r2.w
/*   18   */     (p0) setp_ne r0._, r1.y
/*   19   */    (!p0) setp_clr r2.___w
/*    2.0 */ (!p0) endloop i31, L2
                label L5
/*    2.1 */       alloc colors
/*    3.0 */       exece
/*   20   */          cndge r0.x___, r0.zzzz, c255.yyyy, r2.xxxx
/*   21   */          add oC0, r0.xxxx, r1.zzzz
/*    3.1 */       cnop
