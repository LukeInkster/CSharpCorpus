﻿#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (c) 2007-2016 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using ShareX.HelpersLib;
using ShareX.UploadersLib.FileUploaders;
using ShareX.UploadersLib.ImageUploaders;
using ShareX.UploadersLib.Properties;
using ShareX.UploadersLib.TextUploaders;
using ShareX.UploadersLib.URLShorteners;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ShareX.UploadersLib
{
    public partial class UploadersConfigForm
    {
        #region Imgur

        private void ImgurAuthOpen()
        {
            try
            {
                OAuth2Info oauth = new OAuth2Info(APIKeys.ImgurClientID, APIKeys.ImgurClientSecret);

                string url = new Imgur(oauth).GetAuthorizationURL();

                if (!string.IsNullOrEmpty(url))
                {
                    Config.ImgurOAuth2Info = oauth;
                    URLHelpers.OpenURL(url);
                    DebugHelper.WriteLine("ImgurAuthOpen - Authorization URL is opened: " + url);
                }
                else
                {
                    DebugHelper.WriteLine("ImgurAuthOpen - Authorization URL is empty.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Resources.UploadersConfigForm_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ImgurAuthComplete(string code)
        {
            try
            {
                if (!string.IsNullOrEmpty(code) && Config.ImgurOAuth2Info != null)
                {
                    bool result = new Imgur(Config.ImgurOAuth2Info).GetAccessToken(code);

                    if (result)
                    {
                        oauth2Imgur.Status = OAuthLoginStatus.LoginSuccessful;
                        MessageBox.Show(Resources.UploadersConfigForm_Login_successful, "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        oauth2Imgur.Status = OAuthLoginStatus.LoginFailed;
                        MessageBox.Show(Resources.UploadersConfigForm_Login_failed, "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }

                    btnImgurRefreshAlbumList.Enabled = result;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Resources.UploadersConfigForm_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ImgurAuthRefresh()
        {
            try
            {
                if (OAuth2Info.CheckOAuth(Config.ImgurOAuth2Info))
                {
                    bool result = new Imgur(Config.ImgurOAuth2Info).RefreshAccessToken();

                    if (result)
                    {
                        oauth2Imgur.Status = OAuthLoginStatus.LoginSuccessful;
                        MessageBox.Show(Resources.UploadersConfigForm_Login_successful, "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        oauth2Imgur.Status = OAuthLoginStatus.LoginFailed;
                        MessageBox.Show(Resources.UploadersConfigForm_Login_failed, "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        atcImgurAccountType.SelectedAccountType = AccountType.Anonymous;
                    }

                    btnImgurRefreshAlbumList.Enabled = result;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Resources.UploadersConfigForm_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ImgurRefreshAlbumList()
        {
            try
            {
                lvImgurAlbumList.Items.Clear();

                if (OAuth2Info.CheckOAuth(Config.ImgurOAuth2Info))
                {
                    Config.ImgurAlbumList = new Imgur(Config.ImgurOAuth2Info).GetAlbums();
                    ImgurFillAlbumList();
                    lvImgurAlbumList.Focus();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Resources.UploadersConfigForm_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ImgurFillAlbumList()
        {
            if (Config.ImgurAlbumList != null)
            {
                foreach (ImgurAlbumData album in Config.ImgurAlbumList)
                {
                    ListViewItem lvi = new ListViewItem(album.id ?? "");
                    lvi.SubItems.Add(album.title ?? "");
                    lvi.SubItems.Add(album.description ?? "");
                    lvi.Selected = Config.ImgurSelectedAlbum != null && !string.IsNullOrEmpty(Config.ImgurSelectedAlbum.id) &&
                        album.id.Equals(Config.ImgurSelectedAlbum.id, StringComparison.InvariantCultureIgnoreCase);
                    lvi.Tag = album;
                    lvImgurAlbumList.Items.Add(lvi);
                }
            }
        }

        #endregion Imgur

        #region Flickr

        public void FlickrAuthOpen()
        {
            try
            {
                FlickrUploader flickr = new FlickrUploader(APIKeys.FlickrKey, APIKeys.FlickrSecret);
                btnFlickrOpenAuthorize.Tag = flickr.GetFrob();
                string url = flickr.GetAuthLink(FlickrPermission.Write);
                if (!string.IsNullOrEmpty(url))
                {
                    URLHelpers.OpenURL(url);
                    btnFlickrCompleteAuth.Enabled = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Resources.UploadersConfigForm_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void FlickrAuthComplete()
        {
            try
            {
                string token = btnFlickrOpenAuthorize.Tag as string;
                if (!string.IsNullOrEmpty(token))
                {
                    FlickrUploader flickr = new FlickrUploader(APIKeys.FlickrKey, APIKeys.FlickrSecret);
                    Config.FlickrAuthInfo = flickr.GetToken(token);
                    pgFlickrAuthInfo.SelectedObject = Config.FlickrAuthInfo;
                    // btnFlickrOpenImages.Text = string.Format("{0}'s photostream", Config.FlickrAuthInfo.Username);
                    MessageBox.Show(Resources.UploadersConfigForm_Login_successful, "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Resources.UploadersConfigForm_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void FlickrCheckToken()
        {
            try
            {
                if (Config.FlickrAuthInfo != null)
                {
                    string token = Config.FlickrAuthInfo.Token;
                    if (!string.IsNullOrEmpty(token))
                    {
                        FlickrUploader flickr = new FlickrUploader(APIKeys.FlickrKey, APIKeys.FlickrSecret);
                        Config.FlickrAuthInfo = flickr.CheckToken(token);
                        pgFlickrAuthInfo.SelectedObject = Config.FlickrAuthInfo;
                        MessageBox.Show(Resources.UploadersConfigForm_Login_successful, "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Resources.UploadersConfigForm_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void FlickrOpenImages()
        {
            if (Config.FlickrAuthInfo != null)
            {
                string userID = Config.FlickrAuthInfo.UserID;
                if (!string.IsNullOrEmpty(userID))
                {
                    FlickrUploader flickr = new FlickrUploader(APIKeys.FlickrKey, APIKeys.FlickrSecret);
                    string url = flickr.GetPhotosLink(userID);
                    if (!string.IsNullOrEmpty(url))
                    {
                        URLHelpers.OpenURL(url);
                    }
                }
            }
        }

        #endregion Flickr

        #region Photobucket

        public void PhotobucketAuthOpen()
        {
            try
            {
                OAuthInfo oauth = new OAuthInfo(APIKeys.PhotobucketConsumerKey, APIKeys.PhotobucketConsumerSecret);

                string url = new Photobucket(oauth).GetAuthorizationURL();

                if (!string.IsNullOrEmpty(url))
                {
                    Config.PhotobucketOAuthInfo = oauth;
                    URLHelpers.OpenURL(url);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Resources.UploadersConfigForm_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void PhotobucketAuthComplete()
        {
            try
            {
                string verification = txtPhotobucketVerificationCode.Text;

                if (!string.IsNullOrEmpty(verification) && Config.PhotobucketOAuthInfo != null &&
                    !string.IsNullOrEmpty(Config.PhotobucketOAuthInfo.AuthToken) && !string.IsNullOrEmpty(Config.PhotobucketOAuthInfo.AuthSecret))
                {
                    Photobucket pb = new Photobucket(Config.PhotobucketOAuthInfo);
                    bool result = pb.GetAccessToken(verification);

                    if (result)
                    {
                        Config.PhotobucketAccountInfo = pb.GetAccountInfo();
                        lblPhotobucketAccountStatus.Text = Resources.UploadersConfigForm_Login_successful;
                        txtPhotobucketDefaultAlbumName.Text = Config.PhotobucketAccountInfo.AlbumID;
                        Config.PhotobucketAccountInfo.AlbumList.Add(Config.PhotobucketAccountInfo.AlbumID);
                        cboPhotobucketAlbumPaths.Items.Add(Config.PhotobucketAccountInfo.AlbumID);
                        cboPhotobucketAlbumPaths.SelectedIndex = 0;
                        MessageBox.Show(Resources.UploadersConfigForm_Login_successful, "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        lblPhotobucketAccountStatus.Text = Resources.UploadersConfigForm_Login_failed;
                        MessageBox.Show(Resources.UploadersConfigForm_Login_failed, "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Resources.UploadersConfigForm_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void PhotobucketCreateAlbum()
        {
            Photobucket pb = new Photobucket(Config.PhotobucketOAuthInfo, Config.PhotobucketAccountInfo);
            if (pb.CreateAlbum(txtPhotobucketParentAlbumPath.Text, txtPhotobucketNewAlbumName.Text))
            {
                string albumPath = txtPhotobucketParentAlbumPath.Text + "/" + txtPhotobucketNewAlbumName.Text;
                Config.PhotobucketAccountInfo.AlbumList.Add(albumPath);
                cboPhotobucketAlbumPaths.Items.Add(albumPath);
                MessageBox.Show(string.Format(Resources.UploadersConfigForm_PhotobucketCreateAlbum__0__successfully_created_, albumPath), "ShareX",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        #endregion Photobucket

        #region Picasa

        public void PicasaAuthOpen()
        {
            try
            {
                OAuth2Info oauth = new OAuth2Info(APIKeys.GoogleClientID, APIKeys.GoogleClientSecret);

                string url = new Picasa(oauth).GetAuthorizationURL();

                if (!string.IsNullOrEmpty(url))
                {
                    Config.PicasaOAuth2Info = oauth;
                    URLHelpers.OpenURL(url);
                    DebugHelper.WriteLine("PicasaAuthOpen - Authorization URL is opened: " + url);
                }
                else
                {
                    DebugHelper.WriteLine("PicasaAuthOpen - Authorization URL is empty.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Resources.UploadersConfigForm_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void PicasaAuthComplete(string code)
        {
            try
            {
                if (!string.IsNullOrEmpty(code) && Config.PicasaOAuth2Info != null)
                {
                    bool result = new GoogleDrive(Config.PicasaOAuth2Info).GetAccessToken(code);

                    if (result)
                    {
                        oauth2Picasa.Status = OAuthLoginStatus.LoginSuccessful;
                        MessageBox.Show(Resources.UploadersConfigForm_Login_successful, "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        oauth2Picasa.Status = OAuthLoginStatus.LoginFailed;
                        MessageBox.Show(Resources.UploadersConfigForm_Login_failed, "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }

                    btnPicasaRefreshAlbumList.Enabled = result;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Resources.UploadersConfigForm_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void PicasaAuthRefresh()
        {
            try
            {
                if (OAuth2Info.CheckOAuth(Config.PicasaOAuth2Info))
                {
                    bool result = new GoogleDrive(Config.PicasaOAuth2Info).RefreshAccessToken();

                    if (result)
                    {
                        oauth2Picasa.Status = OAuthLoginStatus.LoginSuccessful;
                        MessageBox.Show(Resources.UploadersConfigForm_Login_successful, "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        oauth2Picasa.Status = OAuthLoginStatus.LoginFailed;
                        MessageBox.Show(Resources.UploadersConfigForm_Login_failed, "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }

                    btnPicasaRefreshAlbumList.Enabled = result;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Resources.UploadersConfigForm_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void PicasaRefreshAlbumList()
        {
            try
            {
                lvPicasaAlbumList.Items.Clear();

                if (OAuth2Info.CheckOAuth(Config.PicasaOAuth2Info))
                {
                    List<PicasaAlbumInfo> albums = new Picasa(Config.PicasaOAuth2Info).GetAlbumList();

                    if (albums != null && albums.Count > 0)
                    {
                        foreach (PicasaAlbumInfo album in albums)
                        {
                            ListViewItem lvi = new ListViewItem(album.ID);
                            lvi.SubItems.Add(album.Name ?? "");
                            lvi.SubItems.Add(album.Summary ?? "");
                            lvi.Tag = album;
                            lvPicasaAlbumList.Items.Add(lvi);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Resources.UploadersConfigForm_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion Picasa

        #region Dropbox

        public void DropboxAuthOpen()
        {
            try
            {
                OAuth2Info oauth = new OAuth2Info(APIKeys.DropboxConsumerKey, APIKeys.DropboxConsumerSecret);

                string url = new Dropbox(oauth).GetAuthorizationURL();

                if (!string.IsNullOrEmpty(url))
                {
                    Config.DropboxOAuth2Info = oauth;
                    URLHelpers.OpenURL(url);
                    DebugHelper.WriteLine("DropboxAuthOpen - Authorization URL is opened: " + url);
                }
                else
                {
                    DebugHelper.WriteLine("DropboxAuthOpen - Authorization URL is empty.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Resources.UploadersConfigForm_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void DropboxAuthComplete(string code)
        {
            try
            {
                if (!string.IsNullOrEmpty(code) && Config.DropboxOAuth2Info != null)
                {
                    Dropbox dropbox = new Dropbox(Config.DropboxOAuth2Info);
                    bool result = dropbox.GetAccessToken(code);

                    if (result)
                    {
                        Config.DropboxAccountInfo = dropbox.GetCurrentAccountAPIv1();
                        UpdateDropboxStatus();

                        oauth2Dropbox.Status = OAuthLoginStatus.LoginSuccessful;

                        if (Config.DropboxAccountInfo != null)
                        {
                            MessageBox.Show(Resources.UploadersConfigForm_Login_successful, "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else
                        {
                            MessageBox.Show(Resources.UploadersConfigForm_DropboxAuthComplete_Login_successful_but_getting_account_info_failed_, "ShareX",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }

                        return;
                    }
                    else
                    {
                        oauth2Dropbox.Status = OAuthLoginStatus.LoginFailed;
                        MessageBox.Show(Resources.UploadersConfigForm_Login_failed, "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }

                Config.DropboxAccountInfo = null;
                UpdateDropboxStatus();
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex);
                MessageBox.Show(ex.ToString(), "ShareX - " + Resources.UploadersConfigForm_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateDropboxStatus()
        {
            if (OAuth2Info.CheckOAuth(Config.DropboxOAuth2Info) && Config.DropboxAccountInfo != null)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine(Resources.UploadersConfigForm_UpdateDropboxStatus_Email_ + " " + Config.DropboxAccountInfo.Email);
                sb.AppendLine(Resources.UploadersConfigForm_UpdateDropboxStatus_Name_ + " " + Config.DropboxAccountInfo.Display_name);
                sb.AppendLine(Resources.UploadersConfigForm_UpdateDropboxStatus_User_ID_ + " " + Config.DropboxAccountInfo.Uid);
                string uploadPath = NameParser.Parse(NameParserType.URL, Dropbox.VerifyPath(Config.DropboxUploadPath));
                sb.AppendLine(Resources.UploadersConfigForm_UpdateDropboxStatus_Upload_path_ + " " + uploadPath);
                sb.AppendLine(Resources.UploadersConfigForm_UpdateDropboxStatus_Download_path_ + " " +
                    Dropbox.GetPublicURL(Config.DropboxAccountInfo.Uid.ToString(), URLHelpers.CombineURL(uploadPath, "Example.png")));
                lblDropboxStatus.Text = sb.ToString();
            }
            else
            {
                lblDropboxStatus.Text = "";
            }
        }

        #endregion Dropbox

        #region Amazon S3

        private void UpdateAmazonS3Status()
        {
            lblAmazonS3PathPreview.Text = new AmazonS3(Config.AmazonS3Settings).GetURL("Example.png");
        }

        #endregion Amazon S3

        #region Google Drive

        public void GoogleDriveAuthOpen()
        {
            try
            {
                OAuth2Info oauth = new OAuth2Info(APIKeys.GoogleClientID, APIKeys.GoogleClientSecret);

                string url = new GoogleDrive(oauth).GetAuthorizationURL();

                if (!string.IsNullOrEmpty(url))
                {
                    Config.GoogleDriveOAuth2Info = oauth;
                    URLHelpers.OpenURL(url);
                    DebugHelper.WriteLine("GoogleDriveAuthOpen - Authorization URL is opened: " + url);
                }
                else
                {
                    DebugHelper.WriteLine("GoogleDriveAuthOpen - Authorization URL is empty.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Resources.UploadersConfigForm_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void GoogleDriveAuthComplete(string code)
        {
            try
            {
                if (!string.IsNullOrEmpty(code) && Config.GoogleDriveOAuth2Info != null)
                {
                    bool result = new GoogleDrive(Config.GoogleDriveOAuth2Info).GetAccessToken(code);

                    if (result)
                    {
                        oauth2GoogleDrive.Status = OAuthLoginStatus.LoginSuccessful;
                        MessageBox.Show(Resources.UploadersConfigForm_Login_successful, "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        oauth2GoogleDrive.Status = OAuthLoginStatus.LoginFailed;
                        MessageBox.Show(Resources.UploadersConfigForm_Login_failed, "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }

                    btnGoogleDriveRefreshFolders.Enabled = result;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Resources.UploadersConfigForm_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void GoogleDriveAuthRefresh()
        {
            try
            {
                if (OAuth2Info.CheckOAuth(Config.GoogleDriveOAuth2Info))
                {
                    bool result = new GoogleDrive(Config.GoogleDriveOAuth2Info).RefreshAccessToken();

                    if (result)
                    {
                        oauth2GoogleDrive.Status = OAuthLoginStatus.LoginSuccessful;
                        MessageBox.Show(Resources.UploadersConfigForm_Login_successful, "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        oauth2GoogleDrive.Status = OAuthLoginStatus.LoginFailed;
                        MessageBox.Show(Resources.UploadersConfigForm_Login_failed, "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }

                    btnGoogleDriveRefreshFolders.Enabled = result;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Resources.UploadersConfigForm_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void GoogleDriveRefreshFolders()
        {
            try
            {
                lvGoogleDriveFoldersList.Items.Clear();

                if (OAuth2Info.CheckOAuth(Config.GoogleDriveOAuth2Info))
                {
                    var folders = new GoogleDrive(Config.GoogleDriveOAuth2Info).GetFolders();

                    if (folders != null)
                    {
                        foreach (var folder in folders)
                        {
                            ListViewItem lvi = new ListViewItem(folder.title);
                            lvi.SubItems.Add(folder.description);
                            lvi.Tag = folder;
                            lvGoogleDriveFoldersList.Items.Add(lvi);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Resources.UploadersConfigForm_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion Google Drive

        #region Box

        public void BoxAuthOpen()
        {
            try
            {
                OAuth2Info oauth = new OAuth2Info(APIKeys.BoxClientID, APIKeys.BoxClientSecret);

                string url = new Box(oauth).GetAuthorizationURL();

                if (!string.IsNullOrEmpty(url))
                {
                    Config.BoxOAuth2Info = oauth;
                    URLHelpers.OpenURL(url);
                    DebugHelper.WriteLine("BoxAuthOpen - Authorization URL is opened: " + url);
                }
                else
                {
                    DebugHelper.WriteLine("BoxAuthOpen - Authorization URL is empty.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Resources.UploadersConfigForm_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void BoxAuthComplete(string code)
        {
            try
            {
                if (!string.IsNullOrEmpty(code) && Config.BoxOAuth2Info != null)
                {
                    bool result = new Box(Config.BoxOAuth2Info).GetAccessToken(code);

                    if (result)
                    {
                        oauth2Box.Status = OAuthLoginStatus.LoginSuccessful;
                        MessageBox.Show(Resources.UploadersConfigForm_Login_successful, "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        oauth2Box.Status = OAuthLoginStatus.LoginFailed;
                        MessageBox.Show(Resources.UploadersConfigForm_Login_failed, "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }

                    btnBoxRefreshFolders.Enabled = result;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Resources.UploadersConfigForm_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void BoxAuthRefresh()
        {
            try
            {
                if (OAuth2Info.CheckOAuth(Config.BoxOAuth2Info))
                {
                    bool result = new Box(Config.BoxOAuth2Info).RefreshAccessToken();

                    if (result)
                    {
                        oauth2Box.Status = OAuthLoginStatus.LoginSuccessful;
                        MessageBox.Show(Resources.UploadersConfigForm_Login_successful, "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        oauth2Box.Status = OAuthLoginStatus.LoginFailed;
                        MessageBox.Show(Resources.UploadersConfigForm_Login_failed, "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }

                    btnBoxRefreshFolders.Enabled = result;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Resources.UploadersConfigForm_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void BoxListFolders()
        {
            lvBoxFolders.Items.Clear();
            BoxAddFolder(Box.RootFolder);
            BoxListFolders(Box.RootFolder);
        }

        public void BoxListFolders(BoxFileEntry fileEntry)
        {
            if (!OAuth2Info.CheckOAuth(Config.BoxOAuth2Info))
            {
                MessageBox.Show(Resources.UploadersConfigForm_ListFolders_Authentication_required_, Resources.UploadersConfigForm_BoxListFolders_Box_refresh_folders_list_failed,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                Box box = new Box(Config.BoxOAuth2Info);
                BoxFileInfo files = box.GetFiles(fileEntry);
                if (files != null && files.entries != null && files.entries.Length > 0)
                {
                    foreach (BoxFileEntry folder in files.entries.Where(x => x.type == "folder"))
                    {
                        BoxAddFolder(folder);
                    }
                }
            }
        }

        private void BoxAddFolder(BoxFileEntry folder)
        {
            ListViewItem lvi = new ListViewItem(folder.name);
            lvi.Tag = folder;
            lvBoxFolders.Items.Add(lvi);
        }

        #endregion Box

        #region OneDrive

        public void OneDriveAuthOpen()
        {
            try
            {
                OAuth2Info oauth = new OAuth2Info(APIKeys.OneDriveClientID, APIKeys.OneDriveClientSecret);
                string url = new OneDrive(oauth).GetAuthorizationURL();

                if (!string.IsNullOrEmpty(url))
                {
                    Config.OneDriveOAuth2Info = oauth;
                    URLHelpers.OpenURL(url);
                    DebugHelper.WriteLine("OneDriveAuthOpen - Authorization URL is opened: " + url);
                }
                else
                {
                    DebugHelper.WriteLine("OneDriveAuthOpen - Authorization URL is empty.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "ShareX - " + Resources.UploadersConfigForm_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void OneDriveAuthComplete(string code)
        {
            try
            {
                if (!string.IsNullOrEmpty(code) && Config.OneDriveOAuth2Info != null)
                {
                    bool result = new OneDrive(Config.OneDriveOAuth2Info).GetAccessToken(code);

                    if (result)
                    {
                        oAuth2OneDrive.Status = OAuthLoginStatus.LoginSuccessful;
                        MessageBox.Show(Resources.UploadersConfigForm_Login_successful, "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        oAuth2OneDrive.Status = OAuthLoginStatus.LoginFailed;
                        MessageBox.Show(Resources.UploadersConfigForm_Login_failed, "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }

                    tvOneDrive.Enabled = result;
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex);
                MessageBox.Show(ex.ToString(), "ShareX - " + Resources.UploadersConfigForm_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void OneDriveAuthRefresh()
        {
            try
            {
                if (OAuth2Info.CheckOAuth(Config.OneDriveOAuth2Info))
                {
                    bool result = new OneDrive(Config.OneDriveOAuth2Info).RefreshAccessToken();

                    if (result)
                    {
                        oAuth2OneDrive.Status = OAuthLoginStatus.LoginSuccessful;
                        MessageBox.Show(Resources.UploadersConfigForm_Login_successful, "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        oAuth2OneDrive.Status = OAuthLoginStatus.LoginFailed;
                        MessageBox.Show(Resources.UploadersConfigForm_Login_failed, "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }

                    tvOneDrive.Enabled = result;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Resources.UploadersConfigForm_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void OneDriveListFolders(OneDriveFileInfo fileEntry, TreeNode tnParent)
        {
            Application.DoEvents();
            OneDrive oneDrive = new OneDrive(Config.OneDriveOAuth2Info);
            OneDrivePathInfo oneDrivePathInfo = oneDrive.GetPathInfo(fileEntry.id);
            tnParent.Nodes.Clear();
            foreach (OneDriveFileInfo folder in oneDrivePathInfo.data.Where(x => x.id.StartsWith("folder.")))
            {
                OneDriveAddFolder(folder, tnParent);
            }
        }

        private void OneDriveAddFolder(OneDriveFileInfo folder, TreeNode tnParent)
        {
            TreeNode tn = new TreeNode(folder.name);
            tn.Tag = folder;
            tn.Nodes.Add(new TreeNode(Resources.UploadersConfigForm_OneDriveAddFolder_Querying_folders___));

            if (tnParent != null)
            {
                tnParent.Nodes.Add(tn);
            }
            else
            {
                tvOneDrive.Nodes.Add(tn);
            }
        }

        #endregion OneDrive

        #region Minus

        public void MinusAuth()
        {
            if (!string.IsNullOrEmpty(txtMinusUsername.Text) && !string.IsNullOrEmpty(txtMinusPassword.Text))
            {
                btnMinusAuth.Enabled = false;
                btnMinusRefreshAuth.Enabled = false;

                try
                {
                    Config.MinusConfig.Username = txtMinusUsername.Text;
                    Config.MinusConfig.Password = txtMinusPassword.Text;
                    Config.MinusOAuth2Info = new OAuth2Info(APIKeys.MinusConsumerKey, APIKeys.MinusConsumerSecret);
                    Minus minus = new Minus(Config.MinusConfig, Config.MinusOAuth2Info);

                    if (minus.GetAccessToken())
                    {
                        minus.ReadFolderList();
                        MinusUpdateControls();
                        MessageBox.Show(Resources.UploadersConfigForm_Login_successful, "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show(Resources.UploadersConfigForm_Login_failed, "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(Resources.UploadersConfigForm_Error + ": " + ex.Message, "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    btnMinusAuth.Enabled = true;
                    btnMinusRefreshAuth.Enabled = true;
                }
            }
        }

        public void MinusAuthRefresh()
        {
            btnMinusAuth.Enabled = false;
            btnMinusRefreshAuth.Enabled = false;

            try
            {
                if (OAuth2Info.CheckOAuth(Config.MinusOAuth2Info))
                {
                    bool result = new Minus(Config.MinusConfig, Config.MinusOAuth2Info).RefreshAccessToken();

                    if (result)
                    {
                        MessageBox.Show(Resources.UploadersConfigForm_Login_successful, "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show(Resources.UploadersConfigForm_Login_failed, "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Resources.UploadersConfigForm_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnMinusAuth.Enabled = true;
                btnMinusRefreshAuth.Enabled = true;
            }
        }

        public void MinusUpdateControls()
        {
            if (Config.MinusConfig != null && Config.MinusConfig.MinusUser != null && OAuth2Info.CheckOAuth(Config.MinusOAuth2Info))
            {
                lblMinusAuthStatus.Text = string.Format(Resources.UploadersConfigForm_MinusUpdateControls_Logged_in_as__0__, Config.MinusConfig.MinusUser.display_name);
                txtMinusUsername.Text = Config.MinusConfig.Username;
                txtMinusPassword.Text = Config.MinusConfig.Password;
                cboMinusFolders.Items.Clear();
                if (Config.MinusConfig.FolderList.Count > 0)
                {
                    cboMinusFolders.Items.AddRange(Config.MinusConfig.FolderList.ToArray());
                    cboMinusFolders.SelectedIndex = Config.MinusConfig.FolderID.BetweenOrDefault(0, cboMinusFolders.Items.Count - 1);
                }
                cbMinusURLType.SelectedIndex = (int)Config.MinusConfig.LinkType;
            }
            else
            {
                lblMinusAuthStatus.Text = Resources.UploadersConfigForm_MinusUpdateControls_Not_logged_in_;
                btnMinusRefreshAuth.Enabled = false;
            }
        }

        private bool MinusHasFolder(string name)
        {
            return cboMinusFolders.Items.Cast<MinusFolder>().Any(mf => mf.name == name);
        }

        #endregion Minus

        #region FTP

        public bool CheckFTPAccounts()
        {
            return Config.FTPAccountList.IsValidIndex(Config.FTPSelectedImage);
        }

        public FTPAccount GetSelectedFTPAccount()
        {
            if (CheckFTPAccounts())
            {
                return Config.FTPAccountList[ucFTPAccounts.lbAccounts.SelectedIndex];
            }

            return null;
        }

        public void AddFTPAccount(FTPAccount account)
        {
            if (account != null)
            {
                Config.FTPAccountList.Add(account);
                ucFTPAccounts.AddItem(account);
                FTPSetup(Config.FTPAccountList);
            }
        }

        public void TestFTPAccountAsync(FTPAccount acc)
        {
            if (acc != null)
            {
                ucFTPAccounts.btnTest.Enabled = false;

                TaskEx.Run(() =>
                {
                    TestFTPAccount(acc);
                },
                () =>
                {
                    ucFTPAccounts.btnTest.Enabled = true;
                });
            }
        }

        private void FTPOpenClient()
        {
            FTPAccount account = GetSelectedFTPAccount();

            if (account != null)
            {
                if (account.Protocol == FTPProtocol.FTP || account.Protocol == FTPProtocol.FTPS)
                {
                    new FTPClientForm(account).Show();
                }
                else
                {
                    MessageBox.Show(Resources.UploadersConfigForm_FTPOpenClient_FTP_client_only_supports_FTP_or_FTPS_, "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return;
            }

            MessageBox.Show(Resources.UploadersConfigForm_FTPOpenClient_Unable_to_find_valid_FTP_account_, "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public static void TestFTPAccount(FTPAccount account)
        {
            string msg = "";
            string remotePath = account.GetSubFolderPath();
            List<string> directories = new List<string>();

            try
            {
                if (account.Protocol == FTPProtocol.FTP || account.Protocol == FTPProtocol.FTPS)
                {
                    using (FTP ftp = new FTP(account))
                    {
                        if (ftp.Connect())
                        {
                            if (!ftp.DirectoryExists(remotePath))
                            {
                                directories = ftp.CreateMultiDirectory(remotePath);
                            }

                            if (ftp.IsConnected)
                            {
                                if (directories.Count > 0)
                                {
                                    msg = Resources.UploadersConfigForm_TestFTPAccount_Connected_Created_folders + "\r\n" + string.Join("\r\n", directories);
                                }
                                else
                                {
                                    msg = Resources.UploadersConfigForm_TestFTPAccount_Connected_;
                                }
                            }
                        }
                    }
                }
                else if (account.Protocol == FTPProtocol.SFTP)
                {
                    using (SFTP sftp = new SFTP(account))
                    {
                        if (sftp.Connect())
                        {
                            if (!sftp.DirectoryExists(remotePath))
                            {
                                directories = sftp.CreateMultiDirectory(remotePath);
                            }

                            if (sftp.IsConnected)
                            {
                                if (directories.Count > 0)
                                {
                                    msg = Resources.UploadersConfigForm_TestFTPAccount_Connected_Created_folders + "\r\n" + string.Join("\r\n", directories);
                                }
                                else
                                {
                                    msg = Resources.UploadersConfigForm_TestFTPAccount_Connected_;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                msg = e.Message;
            }

            MessageBox.Show(msg, "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        #endregion FTP

        #region SendSpace

        public UserPassBox SendSpaceRegister()
        {
            UserPassBox upb = new UserPassBox(Resources.UploadersConfigForm_SendSpaceRegister_SendSpace_Registration___, "John Doe", "john.doe@gmail.com", "JohnDoe", "");
            upb.ShowDialog();
            if (upb.DialogResult == DialogResult.OK)
            {
                SendSpace sendSpace = new SendSpace(APIKeys.SendSpaceKey);
                upb.Success = sendSpace.AuthRegister(upb.UserName, upb.FullName, upb.Email, upb.Password);
                if (!upb.Success && sendSpace.Errors.Count > 0)
                {
                    MessageBox.Show(sendSpace.ToErrorString(), Resources.UploadersConfigForm_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            return upb;
        }

        #endregion SendSpace

        #region Ge.tt

        public void Ge_ttLogin()
        {
            try
            {
                Ge_tt gett = new Ge_tt(APIKeys.Ge_ttKey);
                Ge_ttLogin login = gett.Login(txtGe_ttEmail.Text, txtGe_ttPassword.Text);
                Config.Ge_ttLogin = login;
                lblGe_ttStatus.Text = Resources.UploadersConfigForm_Login_successful;
            }
            catch (Exception ex)
            {
                Config.Ge_ttLogin = null;
                lblGe_ttStatus.Text = Resources.UploadersConfigForm_Login_failed;
                MessageBox.Show(ex.ToString(), Resources.UploadersConfigForm_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion Ge.tt

        #region Pastebin

        public void PastebinLogin()
        {
            if (Config.PastebinSettings != null)
            {
                try
                {
                    Pastebin pastebin = new Pastebin(APIKeys.PastebinKey, Config.PastebinSettings);

                    if (pastebin.Login())
                    {
                        UpdatePastebinStatus();
                        MessageBox.Show(Resources.UploadersConfigForm_Login_successful, "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        UpdatePastebinStatus();
                        MessageBox.Show(Resources.UploadersConfigForm_Login_failed, "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString(), Resources.UploadersConfigForm_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        public void UpdatePastebinStatus()
        {
            if (Config.PastebinSettings == null || string.IsNullOrEmpty(Config.PastebinSettings.UserKey))
            {
                lblPastebinLoginStatus.Text = Resources.OAuthControl_Status_Status__Not_logged_in_;
            }
            else
            {
                lblPastebinLoginStatus.Text = Resources.OAuthControl_Status_Status__Logged_in_;
            }
        }

        #endregion Pastebin

        #region Pushbullet

        public void PushbulletGetDevices()
        {
            cboPushbulletDevices.Items.Clear();
            cboPushbulletDevices.ResetText();

            Pushbullet pushbullet = new Pushbullet(Config.PushbulletSettings);
            Config.PushbulletSettings.DeviceList = pushbullet.GetDeviceList();

            if (Config.PushbulletSettings.DeviceList.Count > 0)
            {
                Config.PushbulletSettings.SelectedDevice = 0;

                cboPushbulletDevices.Enabled = true;

                Config.PushbulletSettings.DeviceList.ForEach(pbDevice =>
                {
                    cboPushbulletDevices.Items.Add(pbDevice.Name ?? Resources.UploadersConfigForm_LoadSettings_Invalid_device_name);
                });

                cboPushbulletDevices.SelectedIndex = 0;
            }
        }

        #endregion Pushbullet

        #region Twitter

        private OAuthInfo GetSelectedTwitterAccount()
        {
            return Config.TwitterOAuthInfoList.ReturnIfValidIndex(Config.TwitterSelectedAccount);
        }

        private bool CheckTwitterAccounts()
        {
            return Config.TwitterOAuthInfoList.IsValidIndex(Config.TwitterSelectedAccount);
        }

        private bool TwitterUpdateSelected()
        {
            Config.TwitterSelectedAccount = lbTwitterAccounts.SelectedIndex;

            if (Config.TwitterSelectedAccount > -1)
            {
                OAuthInfo oauth = Config.TwitterOAuthInfoList[Config.TwitterSelectedAccount];

                if (oauth != null)
                {
                    txtTwitterDescription.Enabled = true;
                    txtTwitterDescription.Text = oauth.Description;
                    oauthTwitter.Enabled = true;

                    if (OAuthInfo.CheckOAuth(oauth))
                    {
                        oauthTwitter.Status = OAuthLoginStatus.LoginSuccessful;
                    }
                    else
                    {
                        oauthTwitter.Status = OAuthLoginStatus.LoginRequired;
                    }

                    return true;
                }
            }

            txtTwitterDescription.Enabled = false;
            txtTwitterDescription.Text = "";
            oauthTwitter.Enabled = false;
            return false;
        }

        private void TwitterAuthOpen()
        {
            if (CheckTwitterAccounts())
            {
                try
                {
                    OAuthInfo oauth = new OAuthInfo(APIKeys.TwitterConsumerKey, APIKeys.TwitterConsumerSecret);

                    string url = new Twitter(oauth).GetAuthorizationURL();

                    if (!string.IsNullOrEmpty(url))
                    {
                        oauth.Description = Config.TwitterOAuthInfoList[Config.TwitterSelectedAccount].Description;
                        Config.TwitterOAuthInfoList[Config.TwitterSelectedAccount] = oauth;
                        URLHelpers.OpenURL(url);
                        DebugHelper.WriteLine("TwitterAuthOpen - Authorization URL is opened: " + url);
                    }
                    else
                    {
                        DebugHelper.WriteLine("TwitterAuthOpen - Authorization URL is empty.");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString(), Resources.UploadersConfigForm_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void TwitterAuthComplete(string code)
        {
            if (CheckTwitterAccounts())
            {
                try
                {
                    OAuthInfo oauth = GetSelectedTwitterAccount();

                    if (oauth != null && !string.IsNullOrEmpty(oauth.AuthToken) && !string.IsNullOrEmpty(oauth.AuthSecret))
                    {
                        bool result = new Twitter(oauth).GetAccessToken(code);

                        if (result)
                        {
                            oauth.AuthVerifier = "";
                            oauthTwitter.Status = OAuthLoginStatus.LoginSuccessful;
                            MessageBox.Show(Resources.UploadersConfigForm_Login_successful, "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else
                        {
                            oauthTwitter.Status = OAuthLoginStatus.LoginFailed;
                            MessageBox.Show(Resources.UploadersConfigForm_Login_failed, "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString(), Resources.UploadersConfigForm_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void TwitterAuthClear()
        {
            if (CheckTwitterAccounts())
            {
                OAuthInfo oauth = new OAuthInfo();

                OAuthInfo oauth2 = GetSelectedTwitterAccount();

                if (oauth2 != null)
                {
                    oauth.Description = oauth2.Description;
                }

                Config.TwitterOAuthInfoList[Config.TwitterSelectedAccount] = oauth;
            }
        }

        #endregion Twitter

        #region goo.gl

        public void GoogleURLShortenerAuthOpen()
        {
            try
            {
                OAuth2Info oauth = new OAuth2Info(APIKeys.GoogleClientID, APIKeys.GoogleClientSecret);

                string url = new GoogleURLShortener(oauth).GetAuthorizationURL();

                if (!string.IsNullOrEmpty(url))
                {
                    Config.GoogleURLShortenerOAuth2Info = oauth;
                    URLHelpers.OpenURL(url);
                    DebugHelper.WriteLine("GoogleURLShortenerAuthOpen - Authorization URL is opened: " + url);
                }
                else
                {
                    DebugHelper.WriteLine("GoogleURLShortenerAuthOpen - Authorization URL is empty.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Resources.UploadersConfigForm_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void GoogleURLShortenerAuthComplete(string code)
        {
            try
            {
                if (!string.IsNullOrEmpty(code) && Config.GoogleURLShortenerOAuth2Info != null)
                {
                    bool result = new GoogleDrive(Config.GoogleURLShortenerOAuth2Info).GetAccessToken(code);

                    if (result)
                    {
                        oauth2GoogleURLShortener.Status = OAuthLoginStatus.LoginSuccessful;
                        MessageBox.Show(Resources.UploadersConfigForm_Login_successful, "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        oauth2GoogleURLShortener.Status = OAuthLoginStatus.LoginFailed;
                        MessageBox.Show(Resources.UploadersConfigForm_Login_failed, "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Resources.UploadersConfigForm_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void GoogleURLShortenerAuthRefresh()
        {
            try
            {
                if (OAuth2Info.CheckOAuth(Config.GoogleURLShortenerOAuth2Info))
                {
                    bool result = new GoogleDrive(Config.GoogleURLShortenerOAuth2Info).RefreshAccessToken();

                    if (result)
                    {
                        oauth2GoogleURLShortener.Status = OAuthLoginStatus.LoginSuccessful;
                        MessageBox.Show(Resources.UploadersConfigForm_Login_successful, "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        oauth2GoogleURLShortener.Status = OAuthLoginStatus.LoginFailed;
                        MessageBox.Show(Resources.UploadersConfigForm_Login_failed, "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Resources.UploadersConfigForm_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion goo.gl

        #region bit.ly

        public void BitlyAuthOpen()
        {
            try
            {
                OAuth2Info oauth = new OAuth2Info(APIKeys.BitlyClientID, APIKeys.BitlyClientSecret);

                string url = new BitlyURLShortener(oauth).GetAuthorizationURL();

                if (!string.IsNullOrEmpty(url))
                {
                    Config.BitlyOAuth2Info = oauth;
                    URLHelpers.OpenURL(url);
                    DebugHelper.WriteLine("BitlyAuthOpen - Authorization URL is opened: " + url);
                }
                else
                {
                    DebugHelper.WriteLine("BitlyAuthOpen - Authorization URL is empty.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Resources.UploadersConfigForm_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void BitlyAuthComplete(string code)
        {
            try
            {
                if (!string.IsNullOrEmpty(code) && Config.BitlyOAuth2Info != null)
                {
                    bool result = new BitlyURLShortener(Config.BitlyOAuth2Info).GetAccessToken(code);

                    if (result)
                    {
                        oauth2Bitly.Status = OAuthLoginStatus.LoginSuccessful;
                        MessageBox.Show(Resources.UploadersConfigForm_Login_successful, "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        oauth2Bitly.Status = OAuthLoginStatus.LoginFailed;
                        MessageBox.Show(Resources.UploadersConfigForm_Login_failed, "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Resources.UploadersConfigForm_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion bit.ly

        #region Custom uploader

        private void UpdateCustomUploader()
        {
            int index = lbCustomUploaderList.SelectedIndex;

            if (index >= 0)
            {
                CustomUploaderItem customUploader = GetCustomUploaderFromFields();

                if (customUploader != null && !string.IsNullOrEmpty(customUploader.Name))
                {
                    Config.CustomUploadersList[index] = customUploader;
                    lbCustomUploaderList.Items[index] = customUploader.Name;
                    PrepareCustomUploaderList();
                }
            }
        }

        private CustomUploaderItem GetSelectedCustomUploader()
        {
            if (lbCustomUploaderList.SelectedIndex >= 0)
            {
                CustomUploaderItem customUploader = GetCustomUploaderFromFields();

                if (customUploader != null && !string.IsNullOrEmpty(customUploader.Name))
                {
                    return customUploader;
                }
            }

            return null;
        }

        private void AddCustomUploader(CustomUploaderItem customUploader)
        {
            if (customUploader != null && !string.IsNullOrEmpty(customUploader.Name))
            {
                Config.CustomUploadersList.Add(customUploader);
                lbCustomUploaderList.Items.Add(customUploader.Name);
                lbCustomUploaderList.SelectedIndex = lbCustomUploaderList.Items.Count - 1;
                PrepareCustomUploaderList();
            }
        }

        private void CustomUploaderClearUploaders()
        {
            Config.CustomUploadersList.Clear();
            lbCustomUploaderList.Items.Clear();
            CustomUploaderClearFields();
            Config.CustomImageUploaderSelected = Config.CustomTextUploaderSelected = Config.CustomFileUploaderSelected = Config.CustomURLShortenerSelected = 0;
            PrepareCustomUploaderList();
        }

        private void CustomUploaderClearFields()
        {
            LoadCustomUploader(new CustomUploaderItem());
        }

        private void CustomUploaderExportAll()
        {
            if (Config.CustomUploadersList != null)
            {
                for (int i = 0; i < lbCustomUploaderList.Items.Count; i++)
                {
                    lbCustomUploaderList.SelectedIndex = i;
                    UpdateCustomUploader();
                }

                using (FolderSelectDialog fsd = new FolderSelectDialog())
                {
                    if (fsd.ShowDialog())
                    {
                        foreach (CustomUploaderItem item in Config.CustomUploadersList)
                        {
                            string json = eiCustomUploaders.Serialize(item);
                            string filepath = Path.Combine(fsd.FileName, item.Name + ".json");
                            File.WriteAllText(filepath, json, Encoding.UTF8);
                        }
                    }
                }
            }
        }

        private void CustomUploaderFixSelectedUploader(int removedIndex)
        {
            if (Config.CustomImageUploaderSelected == removedIndex) Config.CustomImageUploaderSelected = 0;
            else if (Config.CustomImageUploaderSelected > removedIndex) Config.CustomImageUploaderSelected--;

            if (Config.CustomTextUploaderSelected == removedIndex) Config.CustomTextUploaderSelected = 0;
            else if (Config.CustomTextUploaderSelected > removedIndex) Config.CustomTextUploaderSelected--;

            if (Config.CustomFileUploaderSelected == removedIndex) Config.CustomFileUploaderSelected = 0;
            else if (Config.CustomFileUploaderSelected > removedIndex) Config.CustomFileUploaderSelected--;

            if (Config.CustomURLShortenerSelected == removedIndex) Config.CustomURLShortenerSelected = 0;
            else if (Config.CustomURLShortenerSelected > removedIndex) Config.CustomURLShortenerSelected--;
        }

        private void PrepareCustomUploaderList()
        {
            cbCustomUploaderImageUploader.Items.Clear();
            cbCustomUploaderTextUploader.Items.Clear();
            cbCustomUploaderFileUploader.Items.Clear();
            cbCustomUploaderURLShortener.Items.Clear();

            foreach (CustomUploaderItem item in Config.CustomUploadersList)
            {
                cbCustomUploaderImageUploader.Items.Add(item);
                cbCustomUploaderTextUploader.Items.Add(item);
                cbCustomUploaderFileUploader.Items.Add(item);
                cbCustomUploaderURLShortener.Items.Add(item);
            }

            if (Config.CustomUploadersList.IsValidIndex(Config.CustomImageUploaderSelected))
            {
                cbCustomUploaderImageUploader.SelectedIndex = Config.CustomImageUploaderSelected;
            }

            if (Config.CustomUploadersList.IsValidIndex(Config.CustomTextUploaderSelected))
            {
                cbCustomUploaderTextUploader.SelectedIndex = Config.CustomTextUploaderSelected;
            }

            if (Config.CustomUploadersList.IsValidIndex(Config.CustomFileUploaderSelected))
            {
                cbCustomUploaderFileUploader.SelectedIndex = Config.CustomFileUploaderSelected;
            }

            if (Config.CustomUploadersList.IsValidIndex(Config.CustomURLShortenerSelected))
            {
                cbCustomUploaderURLShortener.SelectedIndex = Config.CustomURLShortenerSelected;
            }
        }

        private void LoadCustomUploader(CustomUploaderItem customUploader)
        {
            txtCustomUploaderName.Text = customUploader.Name ?? "";

            cbCustomUploaderRequestType.SelectedIndex = (int)customUploader.RequestType;
            txtCustomUploaderRequestURL.Text = customUploader.RequestURL ?? "";
            txtCustomUploaderFileForm.Text = customUploader.FileFormName ?? "";
            txtCustomUploaderFileForm.Enabled = customUploader.RequestType == CustomUploaderRequestType.POST;

            txtCustomUploaderArgName.Text = "";
            txtCustomUploaderArgValue.Text = "";
            lvCustomUploaderArguments.Items.Clear();
            if (customUploader.Arguments != null)
            {
                foreach (KeyValuePair<string, string> arg in customUploader.Arguments)
                {
                    lvCustomUploaderArguments.Items.Add(arg.Key).SubItems.Add(arg.Value);
                }
            }

            txtCustomUploaderHeaderName.Text = "";
            txtCustomUploaderHeaderValue.Text = "";
            lvCustomUploaderHeaders.Items.Clear();
            if (customUploader.Headers != null)
            {
                foreach (KeyValuePair<string, string> arg in customUploader.Headers)
                {
                    lvCustomUploaderHeaders.Items.Add(arg.Key).SubItems.Add(arg.Value);
                }
            }

            cbCustomUploaderResponseType.SelectedIndex = (int)customUploader.ResponseType;
            txtCustomUploaderRegexp.Text = "";
            lvCustomUploaderRegexps.Items.Clear();
            if (customUploader.RegexList != null)
            {
                foreach (string regexp in customUploader.RegexList)
                {
                    lvCustomUploaderRegexps.Items.Add(regexp);
                }
            }
            txtCustomUploaderJsonPath.Text = "";
            txtCustomUploaderXPath.Text = "";

            txtCustomUploaderURL.Text = customUploader.URL ?? "";
            txtCustomUploaderThumbnailURL.Text = customUploader.ThumbnailURL ?? "";
            txtCustomUploaderDeletionURL.Text = customUploader.DeletionURL ?? "";
        }

        private CustomUploaderItem GetCustomUploaderFromFields()
        {
            CustomUploaderItem item = new CustomUploaderItem(txtCustomUploaderName.Text);

            item.RequestType = (CustomUploaderRequestType)cbCustomUploaderRequestType.SelectedIndex;

            item.RequestURL = txtCustomUploaderRequestURL.Text;

            if (!string.IsNullOrEmpty(txtCustomUploaderFileForm.Text))
            {
                item.FileFormName = txtCustomUploaderFileForm.Text;
            }

            if (lvCustomUploaderArguments.Items.Count > 0)
            {
                item.Arguments = new Dictionary<string, string>();

                foreach (ListViewItem lvItem in lvCustomUploaderArguments.Items)
                {
                    item.Arguments.Add(lvItem.Text, lvItem.SubItems[1].Text);
                }
            }

            if (lvCustomUploaderHeaders.Items.Count > 0)
            {
                item.Headers = new Dictionary<string, string>();

                foreach (ListViewItem lvItem in lvCustomUploaderHeaders.Items)
                {
                    item.Headers.Add(lvItem.Text, lvItem.SubItems[1].Text);
                }
            }

            item.ResponseType = (ResponseType)cbCustomUploaderResponseType.SelectedIndex;

            if (lvCustomUploaderRegexps.Items.Count > 0)
            {
                item.RegexList = new List<string>();

                foreach (ListViewItem lvItem in lvCustomUploaderRegexps.Items)
                {
                    item.RegexList.Add(lvItem.Text);
                }
            }

            if (!string.IsNullOrEmpty(txtCustomUploaderURL.Text))
            {
                item.URL = txtCustomUploaderURL.Text;
            }

            if (!string.IsNullOrEmpty(txtCustomUploaderThumbnailURL.Text))
            {
                item.ThumbnailURL = txtCustomUploaderThumbnailURL.Text;
            }

            if (!string.IsNullOrEmpty(txtCustomUploaderDeletionURL.Text))
            {
                item.DeletionURL = txtCustomUploaderDeletionURL.Text;
            }

            return item;
        }

        private void TestCustomUploader(CustomUploaderType type, CustomUploaderItem item)
        {
            btnCustomUploaderImageUploaderTest.Enabled = btnCustomUploaderTextUploaderTest.Enabled =
                btnCustomUploaderFileUploaderTest.Enabled = btnCustomUploaderURLShortenerTest.Enabled = false;

            UploadResult result = null;

            txtCustomUploaderLog.ResetText();

            TaskEx.Run(() =>
            {
                try
                {
                    switch (type)
                    {
                        case CustomUploaderType.Image:
                            using (Stream stream = ShareXResources.Logo.GetStream())
                            {
                                CustomImageUploader imageUploader = new CustomImageUploader(item);
                                result = imageUploader.Upload(stream, "Test.png");
                                result.Errors = imageUploader.Errors;
                            }
                            break;
                        case CustomUploaderType.Text:
                            CustomTextUploader textUploader = new CustomTextUploader(item);
                            result = textUploader.UploadText("ShareX text upload test", "Test.txt");
                            result.Errors = textUploader.Errors;
                            break;
                        case CustomUploaderType.File:
                            using (Stream stream = ShareXResources.Logo.GetStream())
                            {
                                CustomFileUploader fileUploader = new CustomFileUploader(item);
                                result = fileUploader.Upload(stream, "Test.png");
                                result.Errors = fileUploader.Errors;
                            }
                            break;
                        case CustomUploaderType.URL:
                            CustomURLShortener urlShortener = new CustomURLShortener(item);
                            result = urlShortener.ShortenURL(Links.URL_WEBSITE);
                            result.Errors = urlShortener.Errors;
                            break;
                    }
                }
                catch (Exception e)
                {
                    result = new UploadResult();
                    result.Errors.Add(e.Message);
                }
            },
            () =>
            {
                if (!IsDisposed)
                {
                    if (result != null)
                    {
                        if ((type != CustomUploaderType.URL && !string.IsNullOrEmpty(result.URL)) || (type == CustomUploaderType.URL && !string.IsNullOrEmpty(result.ShortenedURL)))
                        {
                            txtCustomUploaderLog.AppendText("URL: " + result + Environment.NewLine);

                            if (!string.IsNullOrEmpty(result.ThumbnailURL))
                            {
                                txtCustomUploaderLog.AppendText("Thumbnail URL: " + result.ThumbnailURL + Environment.NewLine);
                            }

                            if (!string.IsNullOrEmpty(result.DeletionURL))
                            {
                                txtCustomUploaderLog.AppendText("Deletion URL: " + result.DeletionURL + Environment.NewLine);
                            }
                        }
                        else if (result.IsError)
                        {
                            txtCustomUploaderLog.AppendText(Resources.UploadersConfigForm_Error + ": " + result.ErrorsToString() + Environment.NewLine);
                        }
                        else
                        {
                            txtCustomUploaderLog.AppendText(Resources.UploadersConfigForm_TestCustomUploader_Error__Result_is_empty_ + Environment.NewLine);
                        }

                        txtCustomUploaderLog.ScrollToCaret();

                        btnCustomUploaderShowLastResponse.Tag = result.Response;
                        btnCustomUploaderShowLastResponse.Enabled = !string.IsNullOrEmpty(result.Response);
                    }

                    btnCustomUploaderImageUploaderTest.Enabled = btnCustomUploaderTextUploaderTest.Enabled =
                        btnCustomUploaderFileUploaderTest.Enabled = btnCustomUploaderURLShortenerTest.Enabled = true;
                }
            });
        }

        #endregion Custom uploader

        #region Jira

        public void JiraAuthOpen()
        {
            try
            {
                OAuthInfo oauth = new OAuthInfo(APIKeys.JiraConsumerKey);
                oauth.SignatureMethod = OAuthInfo.OAuthInfoSignatureMethod.RSA_SHA1;
                oauth.ConsumerPrivateKey = Jira.PrivateKey;

                string url = new Jira(Config.JiraHost, oauth).GetAuthorizationURL();

                if (!string.IsNullOrEmpty(url))
                {
                    Config.JiraOAuthInfo = oauth;
                    URLHelpers.OpenURL(url);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Resources.UploadersConfigForm_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void JiraAuthComplete(string code)
        {
            try
            {
                if (!string.IsNullOrEmpty(code) && Config.JiraOAuthInfo != null && !string.IsNullOrEmpty(Config.JiraOAuthInfo.AuthToken) && !string.IsNullOrEmpty(Config.JiraOAuthInfo.AuthSecret))
                {
                    Jira jira = new Jira(Config.JiraHost, Config.JiraOAuthInfo);
                    bool result = jira.GetAccessToken(code);

                    if (result)
                    {
                        oAuthJira.Status = OAuthLoginStatus.LoginSuccessful;
                        MessageBox.Show(Resources.UploadersConfigForm_Login_successful, "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        oAuthJira.Status = OAuthLoginStatus.LoginFailed;
                        MessageBox.Show(Resources.UploadersConfigForm_Login_failed, "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Resources.UploadersConfigForm_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion Jira

        #region Gist

        public void GistAuthOpen()
        {
            try
            {
                OAuth2Info oauth = new OAuth2Info(APIKeys.GitHubID, APIKeys.GitHubSecret);
                string url = new GitHubGist(oauth).GetAuthorizationURL();

                if (!string.IsNullOrEmpty(url))
                {
                    Config.GistOAuth2Info = oauth;
                    URLHelpers.OpenURL(url);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Resources.UploadersConfigForm_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void GistAuthComplete(string code)
        {
            try
            {
                if (!string.IsNullOrEmpty(code) && Config.GistOAuth2Info != null)
                {
                    bool result = new GitHubGist(Config.GistOAuth2Info).GetAccessToken(code);

                    if (result)
                    {
                        oAuth2Gist.Status = OAuthLoginStatus.LoginSuccessful;
                        MessageBox.Show(Resources.UploadersConfigForm_Login_successful, "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        oAuth2Gist.Status = OAuthLoginStatus.LoginFailed;
                        MessageBox.Show(Resources.UploadersConfigForm_Login_failed, "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        atcGistAccountType.SelectedAccountType = AccountType.Anonymous;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Resources.UploadersConfigForm_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion Gist
    }
}