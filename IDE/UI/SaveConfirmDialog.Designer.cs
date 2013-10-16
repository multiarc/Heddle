namespace IDE.UI
{
    partial class SaveConfirmDialog
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.LabelInfoMessage = new System.Windows.Forms.Label();
            this.LabelFileName = new System.Windows.Forms.Label();
            this.ButtonYes = new System.Windows.Forms.Button();
            this.ButtonNo = new System.Windows.Forms.Button();
            this.ButtonYesForAll = new System.Windows.Forms.Button();
            this.ButtonNoForAll = new System.Windows.Forms.Button();
            this.ButtonCancel = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // LabelInfoMessage
            // 
            this.LabelInfoMessage.AutoSize = true;
            this.LabelInfoMessage.Location = new System.Drawing.Point(14, 10);
            this.LabelInfoMessage.Name = "LabelInfoMessage";
            this.LabelInfoMessage.Size = new System.Drawing.Size(227, 15);
            this.LabelInfoMessage.TabIndex = 0;
            this.LabelInfoMessage.Text = "Do you want to save the following file?";
            // 
            // LabelFileName
            // 
            this.LabelFileName.AutoSize = true;
            this.LabelFileName.Location = new System.Drawing.Point(14, 36);
            this.LabelFileName.Name = "LabelFileName";
            this.LabelFileName.Size = new System.Drawing.Size(61, 15);
            this.LabelFileName.TabIndex = 1;
            this.LabelFileName.Text = "FileName";
            // 
            // ButtonYes
            // 
            this.ButtonYes.DialogResult = System.Windows.Forms.DialogResult.Yes;
            this.ButtonYes.Location = new System.Drawing.Point(14, 112);
            this.ButtonYes.Name = "ButtonYes";
            this.ButtonYes.Size = new System.Drawing.Size(61, 27);
            this.ButtonYes.TabIndex = 2;
            this.ButtonYes.Text = "Yes";
            this.ButtonYes.UseVisualStyleBackColor = true;
            // 
            // ButtonNo
            // 
            this.ButtonNo.DialogResult = System.Windows.Forms.DialogResult.No;
            this.ButtonNo.Location = new System.Drawing.Point(81, 112);
            this.ButtonNo.Name = "ButtonNo";
            this.ButtonNo.Size = new System.Drawing.Size(60, 27);
            this.ButtonNo.TabIndex = 3;
            this.ButtonNo.Text = "No";
            this.ButtonNo.UseVisualStyleBackColor = true;
            // 
            // ButtonYesForAll
            // 
            this.ButtonYesForAll.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.ButtonYesForAll.Location = new System.Drawing.Point(166, 113);
            this.ButtonYesForAll.Name = "ButtonYesForAll";
            this.ButtonYesForAll.Size = new System.Drawing.Size(87, 27);
            this.ButtonYesForAll.TabIndex = 4;
            this.ButtonYesForAll.Text = "Yes for All";
            this.ButtonYesForAll.UseVisualStyleBackColor = true;
            // 
            // ButtonNoForAll
            // 
            this.ButtonNoForAll.DialogResult = System.Windows.Forms.DialogResult.Ignore;
            this.ButtonNoForAll.Location = new System.Drawing.Point(259, 112);
            this.ButtonNoForAll.Name = "ButtonNoForAll";
            this.ButtonNoForAll.Size = new System.Drawing.Size(87, 27);
            this.ButtonNoForAll.TabIndex = 5;
            this.ButtonNoForAll.Text = "No for All";
            this.ButtonNoForAll.UseVisualStyleBackColor = true;
            // 
            // ButtonCancel
            // 
            this.ButtonCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.ButtonCancel.Location = new System.Drawing.Point(417, 112);
            this.ButtonCancel.Name = "ButtonCancel";
            this.ButtonCancel.Size = new System.Drawing.Size(87, 27);
            this.ButtonCancel.TabIndex = 6;
            this.ButtonCancel.Text = "Cancel";
            this.ButtonCancel.UseVisualStyleBackColor = true;
            // 
            // SaveConfirmDialog
            // 
            this.AcceptButton = this.ButtonYesForAll;
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.ButtonCancel;
            this.ClientSize = new System.Drawing.Size(516, 152);
            this.ControlBox = false;
            this.Controls.Add(this.ButtonCancel);
            this.Controls.Add(this.ButtonNoForAll);
            this.Controls.Add(this.ButtonYesForAll);
            this.Controls.Add(this.ButtonNo);
            this.Controls.Add(this.ButtonYes);
            this.Controls.Add(this.LabelFileName);
            this.Controls.Add(this.LabelInfoMessage);
            this.Font = new System.Drawing.Font("Constantia", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SaveConfirmDialog";
            this.Text = "Confirm Save";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label LabelInfoMessage;
        internal System.Windows.Forms.Label LabelFileName;
        private System.Windows.Forms.Button ButtonYes;
        private System.Windows.Forms.Button ButtonNo;
        internal System.Windows.Forms.Button ButtonYesForAll;
        internal System.Windows.Forms.Button ButtonNoForAll;
        private System.Windows.Forms.Button ButtonCancel;
    }
}